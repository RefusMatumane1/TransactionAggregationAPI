using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Contracts;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using NSubstitute;
using SharedKernel.Common.ValueObjects;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

/// <summary>
/// The bank decides when a transaction is settled: posted (or no status) is stored
/// Settled; pending is stored Pending and settled in place when the same external id
/// later arrives as posted — with the bank's posted amount/date.
/// </summary>
public class SettlementIngestionTests
{
    private static readonly DateTime PendingDate = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PostedDate = new(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);

    private sealed record Sut(
        ProcessInboundTransactionsCommandHandler Handler,
        TransactionsDbContext Context,
        MessagingDbContext Messaging,
        string ExternalAccountId);

    private static async Task<Sut> BuildAsync()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
        IBankLinksDbContext bankLinks = InMemoryBankLinksDbContextFactory.Create();
        var link = BankLink.Create(CustomerId.Create(), Institution.FNB);
        link.Activate(AccountId.Create(), "ext-acc-1", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
        bankLinks.BankLinks.Add(link);
        await bankLinks.SaveChangesAsync();

        var categorization = Substitute.For<ITransactionCategorizationService>();
        categorization.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
            .Returns(TransactionCategory.Uncategorized);

        var handler = new ProcessInboundTransactionsCommandHandler(
            context, messaging, new BankLinksReadApi(bankLinks), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, categorization,
            NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
        return new Sut(handler, context, messaging, "ext-acc-1");
    }

    private static ExternalTransactionDTO Dto(string id, string? status, decimal amount = -150m, DateTime? date = null) => new()
    {
        Id = id,
        Amount = amount,
        Currency = "ZAR",
        Description = "Coffee shop",
        Category = string.Empty,
        Date = date ?? PendingDate,
        Status = status
    };

    private static Task<SharedKernel.Common.Models.Result<int>> Deliver(Sut sut, params ExternalTransactionDTO[] dtos) =>
        sut.Handler.Handle(new ProcessInboundTransactionsCommand("source-1", sut.ExternalAccountId, dtos), CancellationToken.None);

    private static List<AuditEventRecord> Audit(Sut sut) =>
        sut.Messaging.OutboxMessages
            .Where(m => m.Type == AuditOutbox.MessageType)
            .AsEnumerable()
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .ToList();

    [Theory]
    [InlineData(null)]
    [InlineData("posted")]
    [InlineData("POSTED")]
    public async Task NewTransaction_PostedOrNoStatus_IsStoredSettled(string? status)
    {
        var sut = await BuildAsync();

        await Deliver(sut, Dto("txn-1", status));

        sut.Context.Transactions.Single().Status.Should().Be(TransactionStatus.Settled);
    }

    [Fact]
    public async Task NewTransaction_Pending_IsStoredPending()
    {
        var sut = await BuildAsync();

        await Deliver(sut, Dto("txn-1", "pending"));

        sut.Context.Transactions.Single().Status.Should().Be(TransactionStatus.Pending);
    }

    [Fact]
    public async Task PendingThenPosted_SettlesTheSameRowWithTheBanksPostedAmountAndDate()
    {
        var sut = await BuildAsync();
        await Deliver(sut, Dto("txn-1", "pending", amount: -150m));
        var id = sut.Context.Transactions.Single().Id;

        var result = await Deliver(sut, Dto("txn-1", "posted", amount: -165m, date: PostedDate));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0, "nothing new was created — an existing row was settled");
        var stored = sut.Context.Transactions.Should().ContainSingle().Subject;
        stored.Id.Should().Be(id);
        stored.Status.Should().Be(TransactionStatus.Settled);
        stored.Amount.Amount.Should().Be(-165m);
        stored.Date.Should().Be(PostedDate);
    }

    [Fact]
    public async Task PendingThenPosted_IsNotReportedAsADuplicate_ButIsAuditedAsSettled()
    {
        var sut = await BuildAsync();
        await Deliver(sut, Dto("txn-1", "pending", amount: -150m));

        await Deliver(sut, Dto("txn-1", "posted", amount: -165m));

        sut.Messaging.OutboxMessages.Should().NotContain(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected);
        var settled = Audit(sut).Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionSettled).Subject;
        settled.ExternalTransactionId.Should().Be("txn-1");
        settled.Metadata.Should().Contain("pendingAmount", "-150").And.Contain("amount", "-165");
        Audit(sut).Last(e => e.EventType == AuditEventTypes.InboundProcessed)
            .Metadata.Should().Contain("settledCount", "1");
    }

    [Fact]
    public async Task PendingThenPosted_InvalidatesTheCustomersCachedTotals()
    {
        var sut = await BuildAsync();
        await Deliver(sut, Dto("txn-1", "pending"));
        var syncedBefore = sut.Messaging.OutboxMessages.Count(m => m.Type == OutboxMessageTypes.TransactionSynced);

        await Deliver(sut, Dto("txn-1", "posted"));

        sut.Messaging.OutboxMessages.Count(m => m.Type == OutboxMessageTypes.TransactionSynced)
            .Should().Be(syncedBefore + 1);
    }

    [Fact]
    public async Task PostedThenStalePending_StaysSettled_AndIsSkippedAsDuplicate()
    {
        var sut = await BuildAsync();
        await Deliver(sut, Dto("txn-1", "posted", amount: -165m));

        await Deliver(sut, Dto("txn-1", "pending", amount: -150m));

        var stored = sut.Context.Transactions.Single();
        stored.Status.Should().Be(TransactionStatus.Settled);
        stored.Amount.Amount.Should().Be(-165m);
        Audit(sut).Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionDuplicateSkipped)
            .Which.Detail.Should().Be("Pending update for a transaction that is already settled");
    }

    [Fact]
    public async Task PendingRedelivered_IsADuplicate()
    {
        var sut = await BuildAsync();
        await Deliver(sut, Dto("txn-1", "pending"));

        await Deliver(sut, Dto("txn-1", "pending"));

        sut.Context.Transactions.Single().Status.Should().Be(TransactionStatus.Pending);
        Audit(sut).Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionDuplicateSkipped);
    }

    [Fact]
    public async Task OneDeliveryCarryingPendingAndPosted_StoresOneSettledRowWithThePostedAmount()
    {
        var sut = await BuildAsync();

        await Deliver(sut, Dto("txn-1", "pending", amount: -150m), Dto("txn-1", "posted", amount: -165m));

        var stored = sut.Context.Transactions.Should().ContainSingle().Subject;
        stored.Status.Should().Be(TransactionStatus.Settled);
        stored.Amount.Amount.Should().Be(-165m);
    }

    [Fact]
    public async Task NewTransaction_AuditRecordsTheStoredStatus()
    {
        var sut = await BuildAsync();

        await Deliver(sut, Dto("txn-1", "pending"), Dto("txn-2", null));

        Audit(sut).Where(e => e.EventType == AuditEventTypes.TransactionIngested)
            .Select(e => (e.ExternalTransactionId, e.Metadata!["status"]))
            .Should().BeEquivalentTo(new[] { ("txn-1", "Pending"), ("txn-2", "Settled") });
    }

    [Theory]
    [InlineData("pending", true)]
    [InlineData("posted", true)]
    [InlineData(null, true)]
    [InlineData("booked", false)]
    [InlineData("", false)]
    public void Validator_AcceptsOnlyPendingPostedOrNoStatus(string? status, bool valid)
    {
        var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1", status)]);

        new ReceiveBankTransactionsCommandValidator().Validate(command).IsValid.Should().Be(valid);
    }
}