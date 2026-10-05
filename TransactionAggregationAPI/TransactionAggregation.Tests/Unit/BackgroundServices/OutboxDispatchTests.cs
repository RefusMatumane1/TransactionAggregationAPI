using BuildingBlocks.Application.Caching;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Publishing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Contracts.IntegrationEvents;
using NSubstitute;
using System.Net;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregation.Worker.BackgroundServices;
using TransactionAggregation.Worker.Notifications;
using TransactionAggregation.Worker.Outbox;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
    public class OutboxDispatchTests
    {
        private static OutboxDispatcherBackgroundService Dispatcher(int maxAttempts = 5) =>
            new(Substitute.For<IServiceScopeFactory>(), NullLogger<OutboxDispatcherBackgroundService>.Instance,
                Options.Create(new OutboxOptions { MaxAttempts = maxAttempts }));

        private static OutboxMessage Recorded(string institution = "FNB", string account = "acc-1") =>
            OutboxMessage.Create(TransactionRecorded.EventType, id => JsonSerializer.Serialize(new TransactionRecorded(
                id, Guid.NewGuid(), institution, account, "bank-1", -10m, "ZAR", "Coffee", "Dining",
                DateTime.UtcNow, DateTime.UtcNow)), TransactionRecorded.SchemaVersion);

        [Fact]
        public async Task TransactionRecorded_IsPublishedWithItsIdAsTheDedupKey_AndPartitionedByAccount()
        {
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            var message = Recorded(account: "acc-42");

            await Dispatcher().ProcessMessageAsync(message, new TransactionRecordedHandler(publisher));

            message.Status.Should().Be(OutboxMessageStatus.Processed);
            await publisher.Received(1).PublishAsync(
                Arg.Is<IntegrationEventEnvelope>(e =>
                    e.MessageId == message.Id.Value
                    && e.Type == TransactionRecorded.EventType
                    && e.SchemaVersion == TransactionRecorded.SchemaVersion
                    && e.PartitionKey == "FNB:acc-42"
                    && e.Payload == message.Payload),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ABatchOfNewEntries_InvalidatesTheCacheOnce_NotOncePerMessage()
        {
            var cache = Substitute.For<ICacheService>();
            var handlers = new Dictionary<string, IOutboxMessageHandler>
            {
                [TransactionRecorded.EventType] = new TransactionRecordedHandler(Substitute.For<IIntegrationEventPublisher>())
            };
            var run = new OutboxDispatchRun(cache);
            var messages = new[] { Recorded(), Recorded(), Recorded() };

            foreach (var message in messages)
            {
                message.Claim(DateTime.UtcNow);
                await Dispatcher().ProcessMessageAsync(message, handlers, run, CancellationToken.None);
            }

            messages.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Processed);
            await cache.Received(1).InvalidateScopeAsync(TransactionCacheScopes.All, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AClaimedBatch_IsPublishedConcurrently_AndCommittedTogether()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var messages = new[] { Recorded(), Recorded(), Recorded() };
            messaging.OutboxMessages.AddRange(messages);
            await messaging.SaveChangesAsync();
            foreach (var message in messages)
                message.Claim(DateTime.UtcNow);

            // Each publish waits until all three are in flight: a one-at-a-time dispatcher never gets there.
            var started = 0;
            var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher.PublishAsync(default!, default).ReturnsForAnyArgs(async _ =>
            {
                if (Interlocked.Increment(ref started) == messages.Length)
                    allStarted.TrySetResult();
                await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            });
            var handlers = new Dictionary<string, IOutboxMessageHandler>
            {
                [TransactionRecorded.EventType] = new TransactionRecordedHandler(publisher)
            };

            await Dispatcher().DispatchAsync(
                messages, handlers, new OutboxDispatchRun(Substitute.For<ICacheService>()), messaging, CancellationToken.None);

            messages.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Processed);
            messaging.ChangeTracker.HasChanges().Should().BeFalse("the chunk's outcomes are saved once it completes");
        }

        [Fact]
        public async Task ConcurrentHandlers_InvalidateTheCacheOnce()
        {
            var cache = Substitute.For<ICacheService>();
            cache.InvalidateScopeAsync(default!, default).ReturnsForAnyArgs(_ => Task.Delay(20));
            var run = new OutboxDispatchRun(cache);

            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                Task.Run(() => run.InvalidateCacheScopeOnceAsync(TransactionCacheScopes.All, CancellationToken.None))));

            await cache.Received(1).InvalidateScopeAsync(TransactionCacheScopes.All, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task BrokerUnavailable_IsRetriedWithBackoff_NotDeadLettered()
        {
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher.PublishAsync(default!, default).ReturnsForAnyArgs(Task.FromException(new TimeoutException("broker down")));
            var message = Recorded();

            await Dispatcher().ProcessMessageAsync(message, new TransactionRecordedHandler(publisher));

            message.Status.Should().Be(OutboxMessageStatus.Pending);
            message.Attempts.Should().Be(1);
            message.NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
        }

        [Fact]
        public async Task BrokerUnavailable_OnTheLastAttempt_IsDeadLettered_NeverRetriedForever()
        {
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher.PublishAsync(default!, default).ReturnsForAnyArgs(Task.FromException(new TimeoutException("broker down")));
            var message = Recorded();

            for (var attempt = 0; attempt < 3; attempt++)
                await Dispatcher(maxAttempts: 3).ProcessMessageAsync(message, new TransactionRecordedHandler(publisher));

            message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
            message.Attempts.Should().Be(3);
        }

        [Fact]
        public async Task ARefusedPublish_IsDeadLetteredAtOnce_WithoutSpendingRetries()
        {
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher.PublishAsync(default!, default).ReturnsForAnyArgs(
                Task.FromException(new PermanentDeliveryException("topic authorization failed")));
            var message = Recorded();

            await Dispatcher().ProcessMessageAsync(message, new TransactionRecordedHandler(publisher));

            message.Status.Should().Be(OutboxMessageStatus.DeadLettered);
            message.Attempts.Should().Be(1);
        }

        [Fact]
        public async Task Shutdown_HandsBackTheClaimsItNeverWorkedOn_WithoutSpendingTheirAttempts()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var messages = new[] { Recorded(), Recorded(), Recorded() };
            messaging.OutboxMessages.AddRange(messages);
            await messaging.SaveChangesAsync();
            foreach (var message in messages)
                message.Claim(DateTime.UtcNow);

            using var shutdown = new CancellationTokenSource();
            var publisher = Substitute.For<IIntegrationEventPublisher>();
            publisher.PublishAsync(default!, default).ReturnsForAnyArgs(_ =>
            {
                shutdown.Cancel();
                return Task.CompletedTask;
            });
            var handlers = new Dictionary<string, IOutboxMessageHandler>
            {
                [TransactionRecorded.EventType] = new TransactionRecordedHandler(publisher)
            };

            var act = () => Dispatcher().DispatchAsync(
                messages, handlers, new OutboxDispatchRun(Substitute.For<ICacheService>()), messaging, shutdown.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            messages[0].Status.Should().Be(OutboxMessageStatus.Processed, "its publish completed before the stop");
            messages.Skip(1).Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending && m.Attempts == 0 && m.ClaimedAt == null);
        }

        [Fact]
        public async Task InboxShutdown_HandsBackTheClaimsItNeverWorkedOn()
        {
            var messaging = InMemoryMessagingDbContextFactory.Create();
            var messages = Enumerable.Range(0, 3)
                .Select(_ => InboxMessage.Create("FNB", """{"ExternalAccountId":"acc-1","Transactions":[]}"""))
                .ToArray();
            messaging.InboxMessages.AddRange(messages);
            await messaging.SaveChangesAsync();
            foreach (var message in messages)
                message.Claim(DateTime.UtcNow);

            using var shutdown = new CancellationTokenSource();
            shutdown.Cancel();
            var dispatcher = new InboxDispatcherBackgroundService(
                Substitute.For<IServiceScopeFactory>(), NullLogger<InboxDispatcherBackgroundService>.Instance, Options.Create(new InboxOptions()));

            var act = () => dispatcher.DispatchAsync(
                messages, Substitute.For<ISender>(), messaging, InMemoryDbContextFactory.Create(messagingDbContext: messaging), shutdown.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            messages.Should().OnlyContain(m => m.Status == InboxMessageStatus.Pending && m.Attempts == 0);
            messaging.ChangeTracker.HasChanges().Should().BeFalse("the release was saved");
        }
    }

    public class DuplicateAlertTests
    {
        private const string HookUrl = "https://hooks.example.test/services/T000/B000/SECRET-TOKEN";

        private static readonly DuplicateInboundDetectedOutboxPayload Duplicate =
            new("transaction", "FNB", "1234567890", ["bank-1"], Guid.NewGuid(), DateTime.UtcNow);

        private static (NotificationService Service, RecordingLogger<NotificationService> Logs) Build(HttpStatusCode status)
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(NotificationService.HttpClientName).Returns(_ => new HttpClient(new StubHandler(status)));
            var logs = new RecordingLogger<NotificationService>();
            return (new NotificationService(logs, factory,
                Options.Create(new NotificationOptions { DuplicateAlertWebhookUrl = HookUrl })), logs);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, FailureKind.Transient)]
        [InlineData(HttpStatusCode.InternalServerError, FailureKind.Transient)]
        [InlineData(HttpStatusCode.BadGateway, FailureKind.Transient)]
        [InlineData(HttpStatusCode.BadRequest, FailureKind.Permanent)]
        [InlineData(HttpStatusCode.Forbidden, FailureKind.Permanent)]
        public async Task AFailedAlert_Throws_SoTheOutboxRetriesOrDeadLettersIt(HttpStatusCode status, FailureKind expected)
        {
            var (service, _) = Build(status);

            var act = () => service.SendDuplicateInboundAlertAsync(Duplicate);

            var thrown = (await act.Should().ThrowAsync<HttpRequestException>()).Which;
            FailureClassifier.Classify(thrown).Should().Be(expected);
        }

        [Fact]
        public async Task ATimeout_IsTransient()
        {
            var factory = Substitute.For<IHttpClientFactory>();
            factory.CreateClient(NotificationService.HttpClientName).Returns(_ => new HttpClient(new StubHandler(timeout: true)));
            var service = new NotificationService(NullLogger<NotificationService>.Instance, factory,
                Options.Create(new NotificationOptions { DuplicateAlertWebhookUrl = HookUrl }));

            var act = () => service.SendDuplicateInboundAlertAsync(Duplicate);

            var thrown = (await act.Should().ThrowAsync<Exception>()).Which;
            FailureClassifier.Classify(thrown).Should().Be(FailureKind.Transient);
        }

        [Fact]
        public async Task TheHookUrl_IsNeverLogged_AndTheAccountIsMasked()
        {
            var (service, logs) = Build(HttpStatusCode.OK);

            await service.SendDuplicateInboundAlertAsync(Duplicate);

            logs.Messages.Should().NotContain(m => m.Contains("SECRET-TOKEN") || m.Contains("hooks.example.test"));
            logs.Messages.Should().NotContain(m => m.Contains("1234567890"));
            logs.Messages.Should().Contain(m => m.Contains("***7890"));
        }

        private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, bool timeout = false) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                timeout
                    ? throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException())
                    : Task.FromResult(new HttpResponseMessage(status));
        }
    }

    public sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}