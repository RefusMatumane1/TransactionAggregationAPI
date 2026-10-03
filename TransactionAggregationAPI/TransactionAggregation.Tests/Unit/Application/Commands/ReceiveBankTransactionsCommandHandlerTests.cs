using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class ReceiveBankTransactionsCommandHandlerTests
    {
        private static ReceiveBankTransactionsCommandHandler BuildHandler(IMessagingDbContext ctx) =>
            new(ctx, InMemoryDbContextFactory.Create(messagingDbContext: (MessagingDbContext)ctx), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

        private static readonly DateTime TransactionDate = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        private static ExternalTransactionDTO MakeDto(string id = "txn-1", decimal amount = -150m) => new()
        {
            Id = id,
            Amount = amount,
            Currency = "ZAR",
            Description = "Woolworths",
            Category = string.Empty,
            Date = TransactionDate
        };

        [Fact]
        public async Task Handle_ValidRequest_CreatesAPendingInboxMessage()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            var result = await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto()]), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.IsDuplicate.Should().BeFalse();
            var message = context.InboxMessages.Should().ContainSingle().Subject;
            message.Id.Value.Should().Be(result.Value.InboxMessageId);
            message.SourceName.Should().Be("test-source");
            message.Status.Should().Be(InboxMessageStatus.Pending);
            message.IdempotencyKey.Should().StartWith("sha256:");
        }

        [Fact]
        public async Task Handle_ValidRequest_PayloadRoundTripsExternalAccountIdAndTransactions()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);
            var dto = MakeDto("txn-42", amount: -75.50m);

            await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-7", null, [dto]), CancellationToken.None);

            var message = context.InboxMessages.Single();
            var payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)!;

            payload.ExternalAccountId.Should().Be("ext-acc-7");
            payload.Transactions.Should().ContainSingle();
            payload.Transactions[0].Id.Should().Be("txn-42");
            payload.Transactions[0].Amount.Should().Be(-75.50m);
        }

        [Fact]
        public async Task Handle_OnlyQueuesTheDelivery_AndPersistsNoTransactions()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            var result = await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "never-linked", null, [MakeDto()]), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            context.InboxMessages.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_IdenticalPayloadRedelivered_ReturnsOriginalInboxMessageAsDuplicate()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);
            var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto()]);

            var first = await handler.Handle(command, CancellationToken.None);
            var redelivered = await handler.Handle(command, CancellationToken.None);

            redelivered.IsSuccess.Should().BeTrue();
            redelivered.Value.IsDuplicate.Should().BeTrue();
            redelivered.Value.InboxMessageId.Should().Be(first.Value.InboxMessageId);
            context.InboxMessages.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_Duplicate_EnqueuesAMessageLevelDuplicateNotification()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);
            var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-a"), MakeDto("txn-b")]);

            var first = await handler.Handle(command, CancellationToken.None);
            await handler.Handle(command, CancellationToken.None);

            var outbox = context.OutboxMessages.Should()
                .ContainSingle(m => m.Type == OutboxMessageTypes.DuplicateInboundDetected).Subject;
            var payload = JsonSerializer.Deserialize<DuplicateInboundDetectedOutboxPayload>(outbox.Payload)!;
            payload.Level.Should().Be("message");
            payload.SourceName.Should().Be("test-source");
            payload.InboxMessageId.Should().Be(first.Value.InboxMessageId);
            payload.DuplicateExternalIds.Should().BeEquivalentTo(["txn-a", "txn-b"]);
        }

        [Fact]
        public async Task Handle_SameExplicitIdempotencyKeyWithDifferentPayload_IsRefused_NotSwallowedAsDuplicate()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-1")], "delivery-1"), CancellationToken.None);
            var second = await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-2")], "delivery-1"), CancellationToken.None);

            second.IsFailure.Should().BeTrue("acknowledging it as a duplicate would silently drop txn-2");
            second.Error.Code.Should().Be(InboxErrors.IdempotencyKeyReusedCode);
            context.InboxMessages.Should().ContainSingle().Which.IdempotencyKey.Should().Be("delivery-1");
        }

        [Fact]
        public async Task Handle_SameExplicitIdempotencyKeyWithSamePayload_IsAcknowledgedAsDuplicate()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-1")], "delivery-1"), CancellationToken.None);
            var retry = await handler.Handle(
                new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-1")], "delivery-1"), CancellationToken.None);

            retry.Value.IsDuplicate.Should().BeTrue();
            context.InboxMessages.Should().ContainSingle();
        }

        [Fact]
        public async Task Handle_SameIdempotencyKeyFromDifferentSources_StoresBoth()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            var fromWebhook = await handler.Handle(
                new ReceiveBankTransactionsCommand("webhook-source", "ext-acc-1", null, [MakeDto()], "delivery-1"), CancellationToken.None);
            var fromKafka = await handler.Handle(
                new ReceiveBankTransactionsCommand("kafka-source", "ext-acc-1", null, [MakeDto()], "delivery-1"), CancellationToken.None);

            fromWebhook.Value.IsDuplicate.Should().BeFalse();
            fromKafka.Value.IsDuplicate.Should().BeFalse();
            context.InboxMessages.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_DifferentPayloadsWithoutKey_AreNotDuplicates()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);

            await handler.Handle(new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-1")]), CancellationToken.None);
            var second = await handler.Handle(new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto("txn-2")]), CancellationToken.None);

            second.Value.IsDuplicate.Should().BeFalse();
            context.InboxMessages.Should().HaveCount(2);
        }

        [Fact]
        public async Task Handle_DuplicateOfADeadLetteredDelivery_RequeuesItInsteadOfDroppingIt()
        {
            var context = InMemoryMessagingDbContextFactory.Create();
            var handler = BuildHandler(context);
            var command = new ReceiveBankTransactionsCommand("test-source", "ext-acc-1", null, [MakeDto()]);

            await handler.Handle(command, CancellationToken.None);
            var stored = context.InboxMessages.Single();
            stored.MarkDeadLettered("Source not authorized");
            await context.SaveChangesAsync();
            stored.Status.Should().Be(InboxMessageStatus.DeadLettered);

            var resent = await handler.Handle(command, CancellationToken.None);

            resent.Value.IsDuplicate.Should().BeTrue();
            resent.Value.Requeued.Should().BeTrue();
            var requeued = context.InboxMessages.Should().ContainSingle().Subject;
            requeued.Status.Should().Be(InboxMessageStatus.Pending);
            requeued.Attempts.Should().Be(0);
        }
    }
}