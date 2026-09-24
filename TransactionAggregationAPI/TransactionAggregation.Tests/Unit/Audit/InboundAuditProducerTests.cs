using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
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
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.BackgroundServices;
using NSubstitute;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Audit;

/// <summary>
/// The Transactions module's side of the audit trail: which audit records each step of
/// inbound processing queues through the outbox (in the same SaveChanges as the change
/// they describe), and that the outbox dispatcher hands them to the Audit module.
/// </summary>
public class InboundAuditProducerTests
{
    private static readonly DateTime TransactionDate = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    private static ExternalTransactionDTO Dto(string id) => new()
    {
        Id = id,
        Amount = -150m,
        Currency = "ZAR",
        Description = "Woolworths",
        Category = string.Empty,
        Date = TransactionDate
    };

    private static List<AuditEventRecord> QueuedAuditEvents(IMessagingDbContext messaging) =>
        messaging.OutboxMessages
            .Where(m => m.Type == AuditOutbox.MessageType)
            .AsEnumerable()
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .ToList();

    private static readonly InboundDelivery WebhookDelivery = new(
        AuditChannels.Webhook,
        new Dictionary<string, string> { ["remoteIp"] = "203.0.113.7", ["userAgent"] = "aggregator/1.0" });

    // ── Receipt ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Receive_NewDelivery_QueuesAReceivedEventWithChannelMetadata()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var handler = new ReceiveBankTransactionsCommandHandler(messaging, NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1"), Dto("txn-2")], "delivery-1", WebhookDelivery),
            CancellationToken.None);

        var received = QueuedAuditEvents(messaging).Should().ContainSingle().Subject;
        received.EventType.Should().Be(AuditEventTypes.InboundReceived);
        received.Channel.Should().Be(AuditChannels.Webhook);
        received.SourceName.Should().Be("source-1");
        received.ExternalAccountId.Should().Be("ext-acc-1");
        received.InboxMessageId.Should().Be(result.Value.InboxMessageId);
        received.IdempotencyKey.Should().Be("delivery-1");
        received.Metadata.Should().Contain("remoteIp", "203.0.113.7");
        received.Metadata.Should().Contain("transactionCount", "2");
        received.Metadata.Should().Contain("idempotencyKeySource", "sender");
        received.Metadata!["payloadSha256"].Should().StartWith("sha256:");
    }

    [Fact]
    public async Task Receive_NewDelivery_StoresTheChannelOnTheInboxRow()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var handler = new ReceiveBankTransactionsCommandHandler(messaging, NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

        await handler.Handle(
            new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1")], Delivery: WebhookDelivery),
            CancellationToken.None);

        messaging.InboxMessages.Single().Channel.Should().Be(AuditChannels.Webhook);
    }

    [Fact]
    public async Task Receive_ReplayedDelivery_QueuesADuplicateEventPointingAtTheOriginal()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var handler = new ReceiveBankTransactionsCommandHandler(messaging, NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);
        var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1")], Delivery: WebhookDelivery);

        var first = await handler.Handle(command, CancellationToken.None);
        await handler.Handle(command, CancellationToken.None);

        var events = QueuedAuditEvents(messaging);
        events.Select(e => e.EventType).Should().Equal(AuditEventTypes.InboundReceived, AuditEventTypes.InboundDuplicate);
        events[1].InboxMessageId.Should().Be(first.Value.InboxMessageId);
        events[1].Metadata.Should().Contain("originalStatus", nameof(InboxMessageStatus.Pending));
        events[1].EventId.Should().NotBe(events[0].EventId);
    }

    [Fact]
    public async Task Receive_ReplayOfDeadLetteredDelivery_QueuesARequeuedEvent()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var handler = new ReceiveBankTransactionsCommandHandler(messaging, NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);
        var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1")], Delivery: WebhookDelivery);

        await handler.Handle(command, CancellationToken.None);
        messaging.InboxMessages.Single().MarkFailed("no link", TimeSpan.Zero, maxAttempts: 1);
        await messaging.SaveChangesAsync();
        await handler.Handle(command, CancellationToken.None);

        QueuedAuditEvents(messaging).Last().EventType.Should().Be(AuditEventTypes.InboundRequeued);
    }

    // ── Processing ─────────────────────────────────────────────────────────

    private static async Task<BankLink> SeedActiveBankLinkAsync(IBankLinksDbContext ctx, CustomerId customerId)
    {
        var link = BankLink.Create(customerId, Institution.FNB);
        link.Activate(AccountId.Create(), "ext-acc-1", "enc-access", "enc-refresh", DateTime.UtcNow.AddHours(1));
        ctx.BankLinks.Add(link);
        await ctx.SaveChangesAsync();
        return link;
    }

    private static ProcessInboundTransactionsCommandHandler BuildProcessHandler(
        Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext context,
        IMessagingDbContext messaging,
        IBankLinksDbContext bankLinks)
    {
        var categorization = Substitute.For<ITransactionCategorizationService>();
        categorization.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
            .Returns(TransactionCategory.Uncategorized);
        return new(context, messaging, new BankLinksReadApi(bankLinks), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, categorization,
            NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
    }

    [Fact]
    public async Task Process_Batch_QueuesPerTransactionEventsAndASummary_AllCarryingTheDeliveryChannel()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
        var bankLinks = InMemoryBankLinksDbContextFactory.Create();
        var customerId = CustomerId.Create();
        await SeedActiveBankLinkAsync(bankLinks, customerId);
        var handler = BuildProcessHandler(context, messaging, bankLinks);
        var inboxId = Guid.NewGuid();

        await handler.Handle(new ProcessInboundTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-0")]), CancellationToken.None);
        var before = QueuedAuditEvents(messaging).Count;

        await handler.Handle(
            new ProcessInboundTransactionsCommand("source-1", "ext-acc-1",
                [Dto("txn-0"), Dto("txn-1"), Dto("txn-1")], inboxId, AuditChannels.Kafka),
            CancellationToken.None);

        var events = QueuedAuditEvents(messaging).Skip(before).ToList();
        events.Should().OnlyContain(e => e.Channel == AuditChannels.Kafka && e.InboxMessageId == inboxId
                                         && e.CustomerId == customerId.Value);

        var ingested = events.Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionIngested).Subject;
        ingested.ExternalTransactionId.Should().Be("txn-1");
        ingested.TransactionId.Should().Be(context.Transactions.Single(t => t.Source.ExternalId == "txn-1").Id.Value);
        ingested.Metadata.Should().ContainKey("bankLinkId");

        events.Where(e => e.EventType == AuditEventTypes.TransactionDuplicateSkipped)
            .Select(e => (e.ExternalTransactionId, e.Detail))
            .Should().BeEquivalentTo(new[]
            {
                ("txn-1", "Repeated within the same delivery"),
                ("txn-0", "Already stored for this customer")
            });

        events.Should().ContainSingle(e => e.EventType == AuditEventTypes.InboundProcessed)
            .Which.Metadata.Should().Contain("ingestedCount", "1").And.Contain("duplicateCount", "2");
    }

    [Fact]
    public async Task Process_NoChannelGiven_RecordsUnknownRatherThanGuessing()
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
        var bankLinks = InMemoryBankLinksDbContextFactory.Create();
        await SeedActiveBankLinkAsync(bankLinks, CustomerId.Create());
        var handler = BuildProcessHandler(context, messaging, bankLinks);

        await handler.Handle(new ProcessInboundTransactionsCommand("source-1", "ext-acc-1", [Dto("txn-1")]), CancellationToken.None);

        QueuedAuditEvents(messaging).Should().OnlyContain(e => e.Channel == AuditChannels.Unknown);
    }

    // ── Dispatchers ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5, AuditEventTypes.InboundProcessingFailed)]
    [InlineData(1, AuditEventTypes.InboundDeadLettered)]
    public async Task InboxDispatcher_HandlerFailure_QueuesAFailureEventForTheDelivery(int maxAttempts, string expectedType)
    {
        var messaging = InMemoryMessagingDbContextFactory.Create();
        var message = InboxMessage.Create("source-1", """{"ExternalAccountId":"ext-acc-1","Transactions":[]}""", "delivery-1", AuditChannels.Kafka);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<int>(Error.NotFound("BankLink", "ext-acc-1")));
        var sut = new InboxDispatcherBackgroundService(
            Substitute.For<IServiceScopeFactory>(),
            NullLogger<InboxDispatcherBackgroundService>.Instance,
            Options.Create(new InboxOptions { MaxAttempts = maxAttempts }));

        await sut.ProcessMessageAsync(message, sender, messaging, CancellationToken.None);

        var queued = messaging.OutboxMessages.Local
            .Where(m => m.Type == AuditOutbox.MessageType)
            .SelectMany(m => JsonSerializer.Deserialize<AuditOutboxPayload>(m.Payload)!.Events)
            .Should().ContainSingle().Subject;
        queued.EventType.Should().Be(expectedType);
        queued.Channel.Should().Be(AuditChannels.Kafka);
        queued.InboxMessageId.Should().Be(message.Id.Value);
        queued.ExternalAccountId.Should().Be("ext-acc-1");
        queued.Metadata.Should().Contain("attempt", "1");
    }

    [Fact]
    public async Task InboxDispatcher_PassesTheInboxChannelToProcessing()
    {
        var message = InboxMessage.Create("source-1", """{"ExternalAccountId":"ext-acc-1","Transactions":[]}""", "delivery-1", AuditChannels.Webhook);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>()).Returns(Result.Success(0));
        var sut = new InboxDispatcherBackgroundService(
            Substitute.For<IServiceScopeFactory>(),
            NullLogger<InboxDispatcherBackgroundService>.Instance,
            Options.Create(new InboxOptions()));

        await sut.ProcessMessageAsync(message, sender, InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<IRequest<Result<int>>>(c =>
                ((ProcessInboundTransactionsCommand)c).Channel == AuditChannels.Webhook
                && ((ProcessInboundTransactionsCommand)c).InboxMessageId == message.Id.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxDispatcher_AuditEventsMessage_IsRecordedThroughTheAuditTrail()
    {
        var record = new AuditEventRecord(Guid.NewGuid(), AuditEventTypes.InboundReceived, DateTime.UtcNow, AuditChannels.Webhook, "source-1");
        var message = OutboxMessage.Create(AuditOutbox.MessageType, JsonSerializer.Serialize(new AuditOutboxPayload([record])));
        var auditTrail = Substitute.For<IAuditTrail>();
        var sut = new OutboxDispatcherBackgroundService(
            Substitute.For<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcherBackgroundService>.Instance,
            Options.Create(new OutboxOptions()));

        await sut.ProcessMessageAsync(
            message,
            Substitute.For<ITransactionsDbContext>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IAnalyticsService>(),
            Substitute.For<INotificationService>(),
            auditTrail,
            CancellationToken.None);

        message.Status.Should().Be(OutboxMessageStatus.Processed);
        await auditTrail.Received(1).RecordAsync(
            Arg.Is<IReadOnlyCollection<AuditEventRecord>>(e => e.Single().EventId == record.EventId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxDispatcher_AuditTrailFails_LeavesTheMessageForRetry()
    {
        var record = new AuditEventRecord(Guid.NewGuid(), AuditEventTypes.InboundReceived, DateTime.UtcNow, AuditChannels.Webhook, "source-1");
        var message = OutboxMessage.Create(AuditOutbox.MessageType, JsonSerializer.Serialize(new AuditOutboxPayload([record])));
        var auditTrail = Substitute.For<IAuditTrail>();
        auditTrail.RecordAsync(Arg.Any<IReadOnlyCollection<AuditEventRecord>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("audit db down")));
        var sut = new OutboxDispatcherBackgroundService(
            Substitute.For<IServiceScopeFactory>(),
            NullLogger<OutboxDispatcherBackgroundService>.Instance,
            Options.Create(new OutboxOptions { MaxAttempts = 5 }));

        await sut.ProcessMessageAsync(
            message,
            Substitute.For<ITransactionsDbContext>(),
            Substitute.For<ICacheService>(),
            Substitute.For<IAnalyticsService>(),
            Substitute.For<INotificationService>(),
            auditTrail,
            CancellationToken.None);

        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.Attempts.Should().Be(1);
    }
}