using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TransactionAggregation.Application.Common.Inbox;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Common.Outbox;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Observability;
using TransactionAggregation.Infrastructure.BackgroundServices;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
    /// <summary>
    /// failure-scenarios.md scenario 17 flagged that a growing pile of dead-lettered
    /// (poison) messages was previously invisible outside a log line — no metric an
    /// alert could ever fire on. These tests pin the counters that close that gap by
    /// driving the dispatchers' internal message-processing methods directly (made
    /// internal + InternalsVisibleTo for exactly this purpose): once a message
    /// actually transitions to DeadLettered, the matching Prometheus counter for its
    /// type/source must have incremented — not just the database status.
    /// </summary>
    public class DeadLetterMetricsTests
    {
        [Fact]
        public async Task OutboxDispatcher_UnknownMessageType_DeadLettersAndIncrementsMetric()
        {
            var messageType = $"UnknownType-{Guid.NewGuid():N}";
            var message = OutboxMessage.Create(messageType, "{}");

            var sut = new OutboxDispatcherBackgroundService(
                Substitute.For<IServiceScopeFactory>(),
                NullLogger<OutboxDispatcherBackgroundService>.Instance,
                Options.Create(new OutboxOptions()));

            await sut.ProcessMessageAsync(
                message,
                Substitute.For<IApplicationDbContext>(),
                Substitute.For<ICacheService>(),
                Substitute.For<IAnalyticsService>(),
                Substitute.For<INotificationService>(),
                CancellationToken.None);

            message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
            DeadLetterMetrics.OutboxMessagesDeadLettered.WithLabels(messageType).Value.Should().Be(1);
        }

        [Fact]
        public async Task InboxDispatcher_HandlerReturnsFailureOnLastAttempt_DeadLettersAndIncrementsMetric()
        {
            var sourceName = $"unit-test-source-{Guid.NewGuid():N}";
            var message = InboxMessage.Create(sourceName, """{"ExternalAccountId":"acc-1","Transactions":[]}""");

            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<int>(Error.Failure("Test.Failure", "simulated handler failure")));

            var sut = new InboxDispatcherBackgroundService(
                Substitute.For<IServiceScopeFactory>(),
                NullLogger<InboxDispatcherBackgroundService>.Instance,
                Options.Create(new InboxOptions { MaxAttempts = 1 }));

            await sut.ProcessMessageAsync(message, sender, CancellationToken.None);

            message.Status.Should().Be(InboxMessageStatus.DeadLettered);
            DeadLetterMetrics.InboxMessagesDeadLettered.WithLabels(sourceName).Value.Should().Be(1);
        }
    }
}
