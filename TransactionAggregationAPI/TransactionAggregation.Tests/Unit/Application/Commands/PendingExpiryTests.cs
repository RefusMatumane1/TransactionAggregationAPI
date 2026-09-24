using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ExpireStalePendingTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.BackgroundServices;
using Modules.Transactions.Infrastructure.Persistence;
using NSubstitute;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using SharedKernel.Exceptions;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class PendingExpiryTests
{
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly DateTime Cutoff = Now.AddDays(-7);

    private sealed record Sut(
        ExpireStalePendingTransactionsCommandHandler Handler,
        TransactionsDbContext Context,
        MessagingDbContext Messaging);

    private static Sut Build()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
        return new Sut(
            new ExpireStalePendingTransactionsCommandHandler(context, messaging, NullLogger<ExpireStalePendingTransactionsCommandHandler>.Instance),
            context, messaging);
    }

    /// <summary>Stores a transaction with a controlled bank date and receipt time (CreatedAt).</summary>
    private static async Task<Transaction> SeedAsync(
        Sut sut, DateTime date, DateTime receivedAt, TransactionStatus status = TransactionStatus.Pending, string? externalId = null)
    {
        var tx = Transaction.Create(CustomerId.Create(), Money.Create(-80m, "ZAR"), "Hotel pre-auth",
            TransactionCategory.Uncategorized, TransactionSource.Create("FNB", externalId ?? Guid.NewGuid().ToString()),
            AccountId.Create(), date);
        if (status == TransactionStatus.Settled)
            tx.Settle();
        sut.Context.Transactions.Add(tx);
        await sut.Context.SaveChangesAsync();

        tx.CreatedAt = receivedAt;
        await sut.Context.SaveChangesAsync();
        sut.Messaging.OutboxMessages.RemoveRange(sut.Messaging.OutboxMessages);
        await sut.Messaging.SaveChangesAsync();
        return tx;
    }

    private static Task<Result<int>> Run(Sut sut, int batchSize = 100) =>
        sut.Handler.Handle(new ExpireStalePendingTransactionsCommand(Cutoff, batchSize), CancellationToken.None);

    // ── Domain ─────────────────────────────────────────────────────────────

    [Fact]
    public void Expire_Pending_BecomesExpired()
    {
        var tx = Transaction.Create(CustomerId.Create(), Money.Create(-1m, "ZAR"), "x",
            TransactionCategory.Uncategorized, TransactionSource.Create("FNB", "ext-1"));

        tx.Expire();

        tx.Status.Should().Be(TransactionStatus.Expired);
    }

    [Theory]
    [InlineData(TransactionStatus.Settled)]
    [InlineData(TransactionStatus.Expired)]
    [InlineData(TransactionStatus.Cancelled)]
    public void Expire_NotPending_Throws(TransactionStatus status)
    {
        var tx = Transaction.Create(CustomerId.Create(), Money.Create(-1m, "ZAR"), "x",
            TransactionCategory.Uncategorized, TransactionSource.Create("FNB", "ext-1"));
        tx.UpdateStatus(status, "test");

        var act = () => tx.Expire();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Settle_Expired_ReinstatesAsSettled()
    {
        var tx = Transaction.Create(CustomerId.Create(), Money.Create(-1m, "ZAR"), "x",
            TransactionCategory.Uncategorized, TransactionSource.Create("FNB", "ext-1"));
        tx.Expire();

        tx.Settle(Money.Create(-2m, "ZAR"));

        tx.Status.Should().Be(TransactionStatus.Settled);
        tx.Amount.Amount.Should().Be(-2m);
    }

    [Fact]
    public void PendingSince_IsTheLaterOfBankDateAndReceipt()
    {
        var tx = Transaction.Create(CustomerId.Create(), Money.Create(-1m, "ZAR"), "x",
            TransactionCategory.Uncategorized, TransactionSource.Create("FNB", "ext-1"), date: Now.AddDays(-30));
        tx.CreatedAt = Now.AddDays(-2);

        tx.PendingSince.Should().Be(Now.AddDays(-2));
    }

    // ── Job ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Run_ExpiresOnlyPendingOlderThanTheWindow()
    {
        var sut = Build();
        var stale = await SeedAsync(sut, Now.AddDays(-10), Now.AddDays(-10));
        var recent = await SeedAsync(sut, Now.AddDays(-2), Now.AddDays(-2));
        var oldButJustReceived = await SeedAsync(sut, Now.AddDays(-30), Now.AddHours(-1));
        var settled = await SeedAsync(sut, Now.AddDays(-10), Now.AddDays(-10), TransactionStatus.Settled);

        var result = await Run(sut);

        result.Value.Should().Be(1);
        sut.Context.Transactions.Single(t => t.Id == stale.Id).Status.Should().Be(TransactionStatus.Expired);
        sut.Context.Transactions.Single(t => t.Id == recent.Id).Status.Should().Be(TransactionStatus.Pending);
        sut.Context.Transactions.Single(t => t.Id == oldButJustReceived.Id).Status.Should().Be(TransactionStatus.Pending,
            "an old authorisation that only just arrived still gets the full window to be posted");
        sut.Context.Transactions.Single(t => t.Id == settled.Id).Status.Should().Be(TransactionStatus.Settled);
    }

    [Fact]
    public async Task Run_RespectsBatchSize_OldestFirst()
    {
        var sut = Build();
        var oldest = await SeedAsync(sut, Now.AddDays(-30), Now.AddDays(-30));
        await SeedAsync(sut, Now.AddDays(-20), Now.AddDays(-20));
        await SeedAsync(sut, Now.AddDays(-10), Now.AddDays(-10));

        var result = await Run(sut, batchSize: 1);

        result.Value.Should().Be(1);
        sut.Context.Transactions.Single(t => t.Status == TransactionStatus.Expired).Id.Should().Be(oldest.Id);
    }

    [Fact]
    public async Task Run_NothingStale_ReturnsZeroAndWritesNothing()
    {
        var sut = Build();
        await SeedAsync(sut, Now.AddDays(-1), Now.AddDays(-1));

        var result = await Run(sut);

        result.Value.Should().Be(0);
        sut.Messaging.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_QueuesCacheInvalidationAndSystemAuditEvents()
    {
        var sut = Build();
        var stale = await SeedAsync(sut, Now.AddDays(-10), Now.AddDays(-10), externalId: "auth-123");

        await Run(sut);

        var expired = sut.Messaging.OutboxMessages.Should()
            .ContainSingle(m => m.Type == OutboxMessageTypes.TransactionsExpired).Subject;
        JsonSerializer.Deserialize<TransactionsExpiredOutboxPayload>(expired.Payload)!
            .CustomerIds.Should().ContainSingle().Which.Should().Be(stale.CustomerId.Value);

        var audit = sut.Messaging.OutboxMessages
            .Where(m => m.Type == AuditOutbox.MessageType)
            .AsEnumerable()
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .Should().ContainSingle().Subject;
        audit.EventType.Should().Be(AuditEventTypes.TransactionExpired);
        audit.Channel.Should().Be(AuditChannels.System);
        audit.SourceName.Should().Be(ExpireStalePendingTransactionsCommandHandler.SourceName);
        audit.TransactionId.Should().Be(stale.Id.Value);
        audit.ExternalTransactionId.Should().Be("auth-123");
        audit.Metadata.Should().ContainKeys("pendingSince", "cutoff", "amount");
    }

    // ── Late posting after expiry ──────────────────────────────────────────

    private static async Task<(ProcessInboundTransactionsCommandHandler Handler, TransactionsDbContext Context, MessagingDbContext Messaging)> BuildIngestionAsync()
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
        return (new ProcessInboundTransactionsCommandHandler(context, messaging, new BankLinksReadApi(bankLinks), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, categorization,
            NullLogger<ProcessInboundTransactionsCommandHandler>.Instance), context, messaging);
    }

    private static ExternalTransactionDTO Dto(string status, decimal amount = -80m) => new()
    {
        Id = "auth-1",
        Amount = amount,
        Currency = "ZAR",
        Description = "Hotel",
        Category = string.Empty,
        Date = Now.AddDays(-10),
        Status = status
    };

    [Fact]
    public async Task PostingAfterExpiry_ReinstatesTheRowAsSettled()
    {
        var (handler, context, messaging) = await BuildIngestionAsync();
        await handler.Handle(new ProcessInboundTransactionsCommand("src", "ext-acc-1", [Dto("pending")]), CancellationToken.None);
        context.Transactions.Single().Expire();
        await context.SaveChangesAsync();

        await handler.Handle(new ProcessInboundTransactionsCommand("src", "ext-acc-1", [Dto("posted", -95m)]), CancellationToken.None);

        var stored = context.Transactions.Should().ContainSingle().Subject;
        stored.Status.Should().Be(TransactionStatus.Settled);
        stored.Amount.Amount.Should().Be(-95m);
        messaging.OutboxMessages
            .Where(m => m.Type == AuditOutbox.MessageType)
            .AsEnumerable()
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionSettled)
            .Which.Metadata.Should().Contain("previousStatus", "Expired");
    }

    [Fact]
    public async Task PendingAfterExpiry_StaysExpired()
    {
        var (handler, context, _) = await BuildIngestionAsync();
        await handler.Handle(new ProcessInboundTransactionsCommand("src", "ext-acc-1", [Dto("pending")]), CancellationToken.None);
        context.Transactions.Single().Expire();
        await context.SaveChangesAsync();

        await handler.Handle(new ProcessInboundTransactionsCommand("src", "ext-acc-1", [Dto("pending")]), CancellationToken.None);

        context.Transactions.Single().Status.Should().Be(TransactionStatus.Expired);
    }

    // ── Background loop ────────────────────────────────────────────────────

    [Fact]
    public async Task BackgroundRun_DrainsFullBatchesUntilOneComesBackShort()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ExpireStalePendingTransactionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(10), Result.Success(10), Result.Success(3));
        var services = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();
        var sut = new PendingExpiryBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PendingExpiryOptions { BatchSize = 10, ExpireAfterDays = 7 }),
            NullLogger<PendingExpiryBackgroundService>.Instance);

        var total = await sut.RunOnceAsync(CancellationToken.None);

        total.Should().Be(23);
        await sender.Received(3).Send(
            Arg.Is<ExpireStalePendingTransactionsCommand>(c => c.BatchSize == 10 && c.Cutoff < DateTime.UtcNow.AddDays(-6)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackgroundRun_StopsOnConflict_AndRetriesNextRun()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ExpireStalePendingTransactionsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<int>(Error.Conflict("raced")));
        var services = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();
        var sut = new PendingExpiryBackgroundService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PendingExpiryOptions { BatchSize = 10 }),
            NullLogger<PendingExpiryBackgroundService>.Instance);

        var total = await sut.RunOnceAsync(CancellationToken.None);

        total.Should().Be(0);
        await sender.Received(1).Send(Arg.Any<ExpireStalePendingTransactionsCommand>(), Arg.Any<CancellationToken>());
    }
}