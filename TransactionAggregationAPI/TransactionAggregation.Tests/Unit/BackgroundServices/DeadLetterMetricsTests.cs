using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Infrastructure.BackgroundServices;
using NSubstitute;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
    /// <summary>
    /// Dead-lettering a message must increment the matching Prometheus counter, not just change its
    /// status. Drives the dispatchers' internal per-message methods directly.
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
                Substitute.For<ITransactionsDbContext>(),
                Substitute.For<ICacheService>(),
                Substitute.For<IAnalyticsService>(),
                Substitute.For<INotificationService>(),
                Substitute.For<IAuditTrail>(),
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

            await sut.ProcessMessageAsync(message, sender, InMemoryMessagingDbContextFactory.Create(), CancellationToken.None);

            message.Status.Should().Be(InboxMessageStatus.DeadLettered);
            DeadLetterMetrics.InboxMessagesDeadLettered.WithLabels(sourceName).Value.Should().Be(1);
        }
    }
}