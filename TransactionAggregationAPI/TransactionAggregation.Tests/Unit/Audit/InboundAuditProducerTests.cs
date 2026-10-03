using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Domain.Enums;
using NSubstitute;
using SharedKernel.Common.Models;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregation.Worker.BackgroundServices;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Audit
{
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

        private static readonly InboundDelivery WebhookDelivery = new(
            AuditChannels.Webhook,
            new Dictionary<string, string> { ["remoteIp"] = "203.0.113.7", ["userAgent"] = "aggregator/1.0" });

        [Fact]
        public async Task Receive_NewDelivery_RecordsAReceivedEventWithChannelMetadata()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var audit = new RecordingAuditTrail();
            var handler = new ReceiveBankTransactionsCommandHandler(
                messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging, auditTrail: audit), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

            var result = await handler.Handle(
                new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1"), Dto("txn-2")], "delivery-1", WebhookDelivery),
                CancellationToken.None);

            messaging.OutboxMessages.Should().BeEmpty("the receipt is audited directly, not through the outbox");
            var received = audit.Recorded.Should().ContainSingle().Subject;
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
            var audit = new RecordingAuditTrail();
            var handler = new ReceiveBankTransactionsCommandHandler(
                messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging, auditTrail: audit), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

            await handler.Handle(
                new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1")], Delivery: WebhookDelivery),
                CancellationToken.None);

            messaging.InboxMessages.Single().Channel.Should().Be(AuditChannels.Webhook);
        }

        [Fact]
        public async Task Receive_ReplayedDelivery_RecordsADuplicateEventPointingAtTheOriginal()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var audit = new RecordingAuditTrail();
            var handler = new ReceiveBankTransactionsCommandHandler(
                messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging, auditTrail: audit), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);
            var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1")], Delivery: WebhookDelivery);

            var first = await handler.Handle(command, CancellationToken.None);
            await handler.Handle(command, CancellationToken.None);

            var events = audit.Recorded;
            events.Select(e => e.EventType).Should().Equal(AuditEventTypes.InboundReceived, AuditEventTypes.InboundDuplicate);
            events[1].InboxMessageId.Should().Be(first.Value.InboxMessageId);
            events[1].Metadata.Should().Contain("originalStatus", nameof(InboxMessageStatus.Pending));
            events[1].EventId.Should().NotBe(events[0].EventId);
        }

        [Fact]
        public async Task Receive_ReplayOfDeadLetteredDelivery_RecordsARequeuedEvent()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var audit = new RecordingAuditTrail();
            var handler = new ReceiveBankTransactionsCommandHandler(
                messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging, auditTrail: audit), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);
            var command = new ReceiveBankTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1")], Delivery: WebhookDelivery);

            await handler.Handle(command, CancellationToken.None);
            messaging.InboxMessages.Single().MarkDeadLettered("no link");
            await messaging.SaveChangesAsync();
            await handler.Handle(command, CancellationToken.None);

            audit.Recorded.Last().EventType.Should().Be(AuditEventTypes.InboundRequeued);
        }

        private static ProcessInboundTransactionsCommandHandler BuildProcessHandler(
            Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext context,
            IMessagingDbContext messaging)
        {
            var categorization = Substitute.For<ITransactionCategorizationService>();
            categorization.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(TransactionCategory.Uncategorized);
            return new(context, messaging, TestNormalizers.Neutral, categorization,
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
        }

        [Fact]
        public async Task Process_Batch_RecordsPerTransactionEventsAndASummary_AllCarryingTheDeliveryChannel()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
            var handler = BuildProcessHandler(context, messaging);
            var inboxId = Guid.NewGuid();

            await handler.Handle(new ProcessInboundTransactionsCommand("FNB", "ext-acc-1", null, [Dto("txn-0")]), CancellationToken.None);
            var before = context.RecordedAudit().Count;

            await handler.Handle(
                new ProcessInboundTransactionsCommand("FNB", "ext-acc-1", null,
                    [Dto("txn-0"), Dto("txn-1"), Dto("txn-1")], inboxId, AuditChannels.Kafka),
                CancellationToken.None);

            messaging.OutboxMessages.Should().NotContain(m => m.Payload.Contains("inbound."),
                "processing audit rows commit with the transactions, not through the outbox");
            var events = context.RecordedAudit().Skip(before).ToList();
            events.Should().OnlyContain(e => e.Channel == AuditChannels.Kafka && e.InboxMessageId == inboxId);

            var ingested = events.Should().ContainSingle(e => e.EventType == AuditEventTypes.TransactionIngested).Subject;
            ingested.ExternalTransactionId.Should().Be("txn-1");
            ingested.TransactionId.Should().Be(context.Transactions.Single(t => t.Source.ExternalId == "txn-1").Id.Value);
            ingested.Metadata.Should().Contain("institution", "FNB");

            events.Where(e => e.EventType == AuditEventTypes.TransactionDuplicateSkipped)
                .Select(e => (e.ExternalTransactionId, e.Detail))
                .Should().BeEquivalentTo(new[]
                {
                    ("txn-1", "Repeated within the same delivery"),
                    ("txn-0", "Already recorded for this account")
                });

            events.Should().ContainSingle(e => e.EventType == AuditEventTypes.InboundProcessed)
                .Which.Metadata.Should().Contain("recordedCount", "1").And.Contain("duplicateCount", "2");
        }

        [Fact]
        public async Task Process_NoChannelGiven_RecordsUnknownRatherThanGuessing()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
            var handler = BuildProcessHandler(context, messaging);

            await handler.Handle(new ProcessInboundTransactionsCommand("source-1", "ext-acc-1", null, [Dto("txn-1")]), CancellationToken.None);

            context.RecordedAudit().Should().NotBeEmpty().And.OnlyContain(e => e.Channel == AuditChannels.Unknown);
        }

        [Theory]
        [InlineData(5, AuditEventTypes.InboundProcessingFailed)]
        [InlineData(1, AuditEventTypes.InboundDeadLettered)]
        public async Task InboxDispatcher_HandlerFailure_CommitsAFailureEventWithTheRetryState(int maxAttempts, string expectedType)
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var message = InboxMessage.Create("source-1", """{"ExternalAccountId":"ext-acc-1","Transactions":[]}""", "delivery-1", AuditChannels.Kafka);
            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<int>(Error.Conflict("Concurrent ingestion kept conflicting")));
            var sut = new InboxDispatcherBackgroundService(
                Substitute.For<IServiceScopeFactory>(),
                NullLogger<InboxDispatcherBackgroundService>.Instance,
                Options.Create(new InboxOptions { MaxAttempts = maxAttempts }));

            var context = InMemoryDbContextFactory.Create(messagingDbContext: messaging);
            message.Claim(DateTime.UtcNow);

            await sut.ProcessMessageAsync(message, sender, messaging, context, CancellationToken.None);

            var queued = context.RecordedAudit().Should().ContainSingle(
                "the failure and its audit row are committed before the next message is processed").Subject;
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
    }
}