using BuildingBlocks.Application.Caching;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using TransactionAggregation.Worker.Outbox;

namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class OutboxDispatcherBackgroundService : PollingBackgroundService
    {
        private const string Queue = "outbox";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxDispatcherBackgroundService> _logger;
        private readonly OutboxOptions _options;

        // While draining, the COUNT queries run once per Interval, not per batch.
        private DateTime _backlogRecordedAt = DateTime.MinValue;

        public OutboxDispatcherBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxDispatcherBackgroundService> logger,
            IOptions<OutboxOptions> options)
            : base(logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override TimeSpan Interval => TimeSpan.FromSeconds(_options.PollIntervalSeconds);

        protected override async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
                .ToDictionary(h => h.MessageType, StringComparer.Ordinal);
            var run = new OutboxDispatchRun(scope.ServiceProvider.GetRequiredService<ICacheService>(), _logger);

            var claimed = await messaging.ClaimOutboxMessagesAsync(
                _options.BatchSize, TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes), _options.MaxAttempts, cancellationToken);

            if (claimed.Count > 0)
            {
                _logger.LogInformation("Claimed {Count} outbox messages", claimed.Count);
                await DispatchAsync(claimed, handlers, run, messaging, cancellationToken);
            }

            var more = claimed.Count >= _options.BatchSize;
            if (!more || DateTime.UtcNow - _backlogRecordedAt >= Interval)
            {
                await RecordBacklogAsync(messaging, cancellationToken);
                _backlogRecordedAt = DateTime.UtcNow;
            }

            return more;
        }

        // Published MaxConcurrency at a time, committed per chunk: a crash re-sends at most one chunk (consumers dedupe),
        // and order within a chunk is not guaranteed.
        internal async Task DispatchAsync(
            IReadOnlyList<OutboxMessage> claimed,
            IReadOnlyDictionary<string, IOutboxMessageHandler> handlers,
            OutboxDispatchRun run,
            IMessagingDbContext messaging,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (var chunk in claimed.Chunk(Math.Max(1, _options.MaxConcurrency)))
                {
                    await Task.WhenAll(chunk.Select(message => ProcessMessageAsync(message, handlers, run, cancellationToken)));
                    await messaging.SaveChangesAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ClaimRelease.ReleaseAsync(
                    claimed.Where(m => m.Status == OutboxMessageStatus.Processing),
                    m => m.ReleaseClaim(), messaging.SaveChangesAsync, _logger, Queue);
                throw;
            }
        }

        internal async Task ProcessMessageAsync(
            OutboxMessage message,
            IReadOnlyDictionary<string, IOutboxMessageHandler> handlers,
            OutboxDispatchRun run,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var activity = MessagingTelemetry.StartConsumerActivity("outbox.dispatch", message.TraceParent);
            activity?.SetTag("messaging.message.id", message.Id.Value);
            activity?.SetTag("messaging.message.type", message.Type);
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = message.CorrelationId ?? activity?.TraceId.ToString(),
                ["OutboxMessageId"] = message.Id.Value,
                ["OutboxMessageType"] = message.Type
            });
            var stopwatch = Stopwatch.StartNew();

            if (!handlers.TryGetValue(message.Type, out var handler))
            {
                _logger.LogError("Outbox message {MessageId} has unknown type {Type} — dead-lettering", message.Id.Value, message.Type);
                RecordFailure(message, $"Unknown message type: {message.Type}", FailureKind.Permanent, stopwatch);
                return;
            }

            if (message.SchemaVersion > handler.HighestReadableSchemaVersion)
            {
                _logger.LogError(
                    "Outbox message {MessageId} ({Type}) has schema version {SchemaVersion}, newer than this dispatcher reads — dead-lettering",
                    message.Id.Value, message.Type, message.SchemaVersion);
                RecordFailure(message, $"Unsupported schema version {message.SchemaVersion} for {message.Type}", FailureKind.Permanent, stopwatch);
                return;
            }

            try
            {
                await handler.HandleAsync(message, run, cancellationToken);

                message.MarkProcessed();
                MessagingTelemetry.ProcessingDuration.WithLabels(Queue, "processed").Observe(stopwatch.Elapsed.TotalSeconds);
                MessagingTelemetry.EndToEndLag.WithLabels(Queue).Observe((DateTime.UtcNow - message.OccurredAt).TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var kind = FailureClassifier.Classify(ex);
                _logger.LogError(ex, "Outbox message {MessageId} ({Type}) failed on attempt {Attempt} ({FailureKind})",
                    message.Id.Value, message.Type, message.Attempts, kind);
                activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                RecordFailure(message, FailureClassifier.Describe(ex), kind, stopwatch);
            }
        }

        private void RecordFailure(OutboxMessage message, string error, FailureKind kind, Stopwatch stopwatch)
        {
            if (kind == FailureKind.Permanent)
                message.MarkDeadLettered(error);
            else
                message.MarkFailed(error, RetryBackoff.After(message.Attempts), _options.MaxAttempts);

            if (message.Status == OutboxMessageStatus.DeadLettered)
            {
                DeadLetterMetrics.OutboxMessagesDeadLettered.WithLabels(message.Type).Inc();
                _logger.LogError("Outbox message {MessageId} ({Type}) dead-lettered after {Attempts} attempt(s): {Error}",
                    message.Id.Value, message.Type, message.Attempts, error);
            }

            MessagingTelemetry.ProcessingDuration.WithLabels(Queue, "failed").Observe(stopwatch.Elapsed.TotalSeconds);
        }

        private static async Task RecordBacklogAsync(IMessagingDbContext messaging, CancellationToken cancellationToken)
        {
            var pending = await messaging.OutboxMessages.CountAsync(m => m.Status == OutboxMessageStatus.Pending, cancellationToken);
            var deadLettered = await messaging.OutboxMessages.CountAsync(m => m.Status == OutboxMessageStatus.DeadLettered, cancellationToken);
            MessagingTelemetry.Backlog.WithLabels(Queue, "pending").Set(pending);
            MessagingTelemetry.Backlog.WithLabels(Queue, "dead_lettered").Set(deadLettered);
        }
    }
}