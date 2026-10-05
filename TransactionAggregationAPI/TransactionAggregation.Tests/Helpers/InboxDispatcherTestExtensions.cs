using BuildingBlocks.Application.Caching;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using MediatR;
using NSubstitute;
using TransactionAggregation.Worker.BackgroundServices;
using TransactionAggregation.Worker.Outbox;

namespace TransactionAggregation.Tests.Helpers
{
    // Unit tests drive the dispatchers message by message. A message is processed only after a claim,
    // which counts the attempt; these helpers claim it the way MessagingDbContext's SQL does.
    internal static class DispatcherTestExtensions
    {
        public static Task ProcessMessageAsync(
            this InboxDispatcherBackgroundService dispatcher, InboxMessage message, ISender sender,
            MessagingDbContext messaging, CancellationToken cancellationToken)
        {
            if (message.Status == InboxMessageStatus.Pending)
                message.Claim(DateTime.UtcNow);

            return dispatcher.ProcessMessageAsync(
                message, sender, messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging), cancellationToken);
        }

        public static Task ProcessMessageAsync(
            this OutboxDispatcherBackgroundService dispatcher, OutboxMessage message, params IOutboxMessageHandler[] handlers) =>
            dispatcher.ProcessMessageAsync(message, Substitute.For<ICacheService>(), handlers);

        public static Task ProcessMessageAsync(
            this OutboxDispatcherBackgroundService dispatcher, OutboxMessage message, ICacheService cache, params IOutboxMessageHandler[] handlers)
        {
            if (message.Status == OutboxMessageStatus.Pending)
                message.Claim(DateTime.UtcNow);

            return dispatcher.ProcessMessageAsync(
                message, handlers.ToDictionary(h => h.MessageType), new OutboxDispatchRun(cache), CancellationToken.None);
        }
    }
}