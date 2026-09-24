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
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
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
/// A joint bank account is linked once per holder, all with the same external account id.
/// Every holder is entitled to its transactions, so each gets their own copy under their
/// own account — with their own duplicate and settlement state.
/// </summary>
public class JointAccountIngestionTests
{
    private const string JointAccount = "joint-acc-1";

    private sealed record Holder(CustomerId CustomerId, AccountId AccountId, BankLink Link);

    private sealed record Sut(
        ProcessInboundTransactionsCommandHandler Handler,
        TransactionsDbContext Context,
        IMessagingDbContext Messaging,
        IBankLinksDbContext BankLinks)
    {
        public Task<SharedKernel.Common.Models.Result<int>> DeliverAsync(params ExternalTransactionDTO[] transactions) =>
            Handler.Handle(new ProcessInboundTransactionsCommand("test-source", JointAccount, transactions), CancellationToken.None);

        public List<Transaction> RowsOf(Holder holder) =>
            Context.Transactions.Where(t => t.CustomerId == holder.CustomerId).ToList();
    }

    private static Sut Build()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
        var bankLinks = InMemoryBankLinksDbContextFactory.Create();

        var categorization = Substitute.For<ITransactionCategorizationService>();
        categorization.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
            .Returns(TransactionCategory.Uncategorized);

        var handler = new ProcessInboundTransactionsCommandHandler(
            context, messaging, new BankLinksReadApi(bankLinks), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, categorization,
            NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

        return new Sut(handler, context, messaging, bankLinks);
    }

    private static async Task<Holder> LinkHolderAsync(IBankLinksDbContext bankLinks)
    {
        var customerId = CustomerId.Create();
        var accountId = AccountId.Create();
        var link = BankLink.Create(customerId, Institution.FNB);
        link.Activate(accountId, JointAccount, "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        bankLinks.BankLinks.Add(link);
        await bankLinks.SaveChangesAsync();
        return new Holder(customerId, accountId, link);
    }

    private static ExternalTransactionDTO Tx(string id, string? status = null) => new()
    {
        Id = id,
        Amount = -100m,
        Currency = "ZAR",
        Description = "Groceries for the house",
        Category = string.Empty,
        Date = DateTime.UtcNow,
        Status = status
    };

    [Fact]
    public async Task Handle_JointAccount_EveryHolderGetsTheirOwnCopyUnderTheirOwnAccount()
    {
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        var bob = await LinkHolderAsync(sut.BankLinks);

        var result = await sut.DeliverAsync(Tx("t1"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2, "one row per holder");
        sut.RowsOf(alice).Should().ContainSingle().Which.AccountId.Should().Be(alice.AccountId);
        sut.RowsOf(bob).Should().ContainSingle().Which.AccountId.Should().Be(bob.AccountId);
    }

    [Fact]
    public async Task Handle_RedeliveryToJointAccount_IsADuplicateForEveryHolder()
    {
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        var bob = await LinkHolderAsync(sut.BankLinks);
        await sut.DeliverAsync(Tx("t1"));

        var redelivery = await sut.DeliverAsync(Tx("t1"));

        redelivery.Value.Should().Be(0);
        sut.RowsOf(alice).Should().HaveCount(1);
        sut.RowsOf(bob).Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_SameDelivery_CanBeNewForOneHolderAndADuplicateForAnother()
    {
        // Bob links after t1 was delivered: a later delivery repeating t1 is a duplicate for
        // Alice, who already has it, but new for Bob.
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        await sut.DeliverAsync(Tx("t1"));
        var bob = await LinkHolderAsync(sut.BankLinks);

        var result = await sut.DeliverAsync(Tx("t1"), Tx("t2"));

        result.Value.Should().Be(3, "Alice gets t2; Bob gets t1 and t2");
        sut.RowsOf(alice).Select(t => t.Source.ExternalId).Should().BeEquivalentTo(["t1", "t2"]);
        sut.RowsOf(bob).Select(t => t.Source.ExternalId).Should().BeEquivalentTo(["t1", "t2"]);
    }

    [Fact]
    public async Task Handle_PostingForAPendingJointTransaction_SettlesEveryHoldersCopy()
    {
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        var bob = await LinkHolderAsync(sut.BankLinks);
        await sut.DeliverAsync(Tx("t1", status: "pending"));

        await sut.DeliverAsync(Tx("t1", status: "posted"));

        sut.RowsOf(alice).Should().ContainSingle().Which.Status.Should().Be(TransactionStatus.Settled);
        sut.RowsOf(bob).Should().ContainSingle().Which.Status.Should().Be(TransactionStatus.Settled);
    }

    [Fact]
    public async Task Handle_HolderWhoRevokedTheirLink_GetsNothingNew()
    {
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        var bob = await LinkHolderAsync(sut.BankLinks);
        bob.Link.Revoke();
        await sut.BankLinks.SaveChangesAsync();

        var result = await sut.DeliverAsync(Tx("t1"));

        result.Value.Should().Be(1);
        sut.RowsOf(alice).Should().HaveCount(1);
        sut.RowsOf(bob).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_JointAccount_AuditsEachHolderUnderTheirOwnCustomerAndLink()
    {
        var sut = Build();
        var alice = await LinkHolderAsync(sut.BankLinks);
        var bob = await LinkHolderAsync(sut.BankLinks);

        await sut.DeliverAsync(Tx("t1"));

        var events = sut.Messaging.OutboxMessages
            .Where(m => m.Type == AuditOutbox.MessageType)
            .AsEnumerable()
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .ToList();

        var processed = events.Where(e => e.EventType == AuditEventTypes.InboundProcessed).ToList();
        processed.Select(e => e.CustomerId).Should().BeEquivalentTo([alice.CustomerId.Value, bob.CustomerId.Value]);
        processed.Select(e => e.Metadata!["bankLinkId"]).Should().BeEquivalentTo([alice.Link.Id.Value.ToString(), bob.Link.Id.Value.ToString()]);

        var ingested = events.Where(e => e.EventType == AuditEventTypes.TransactionIngested).ToList();
        ingested.Should().HaveCount(2);
        ingested.Select(e => e.TransactionId).Should().OnlyHaveUniqueItems("each holder's copy is its own transaction");
    }
}