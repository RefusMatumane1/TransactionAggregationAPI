using BuildingBlocks.Application.Logging;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using System.Diagnostics;
using System.Text.Json;

namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class InboxDispatcherBackgroundService : PollingBackgroundService
    {
        private const string Queue = "inbox";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InboxDispatcherBackgroundService> _logger;
        private readonly InboxOptions _options;

        // While draining a backlog, the two COUNT queries run once per Interval, not once per batch.
        private DateTime _backlogRecordedAt = DateTime.MinValue;

        public InboxDispatcherBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<InboxDispatcherBackgroundService> logger,
            IOptions<InboxOptions> options)
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
            var context = scope.ServiceProvider.GetRequiredService<ITransactionsDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var claimed = await messaging.ClaimInboxMessagesAsync(
                _options.BatchSize, TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes), _options.MaxAttempts, cancellationToken);

            if (claimed.Count > 0)
            {
                _logger.LogInformation("Claimed {Count} inbox messages", claimed.Count);
                await DispatchAsync(claimed, sender, messaging, context, cancellationToken);
            }

            var more = claimed.Count >= _options.BatchSize;
            if (!more || DateTime.UtcNow - _backlogRecordedAt >= Interval)
            {
                await RecordBacklogAsync(messaging, cancellationToken);
                _backlogRecordedAt = DateTime.UtcNow;
            }

            return more;
        }

        internal async Task DispatchAsync(
            IReadOnlyList<InboxMessage> claimed,
            ISender sender,
            IMessagingDbContext messaging,
            ITransactionsDbContext context,
            CancellationToken cancellationToken)
        {
            var next = 0;
            try
            {
                for (; next < claimed.Count; next++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await ProcessMessageAsync(claimed[next], sender, messaging, context, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ClaimRelease.ReleaseAsync(
                    claimed.Skip(next), m => m.ReleaseClaim(), messaging.SaveChangesAsync, _logger, Queue);
                throw;
            }
        }

        // Success: the handler commits the ledger rows, outbox rows, audit rows and this message's
        // Processed mark in one transaction. Failure: the retry or dead-letter state and its audit
        // row are committed here, before the next message, so no later rollback can discard them.
        internal async Task ProcessMessageAsync(
            InboxMessage message, ISender sender, IMessagingDbContext messaging, ITransactionsDbContext context,
            CancellationToken cancellationToken)
        {
            using var activity = MessagingTelemetry.StartConsumerActivity("inbox.process", message.TraceParent);
            activity?.SetTag("messaging.message.id", message.Id.Value);
            if (message.CorrelationId is not null)
                activity?.SetBaggage(MessagingTelemetry.CorrelationBaggageKey, message.CorrelationId);
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = message.CorrelationId ?? activity?.TraceId.ToString(),
                ["InboxMessageId"] = message.Id.Value,
                ["SourceName"] = message.SourceName,
                ["Attempt"] = message.Attempts
            });

            var stopwatch = Stopwatch.StartNew();
            InboundTransactionsPayload? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)
                    ?? throw new PoisonMessageException($"Inbox message {message.Id.Value} has an empty payload");

                var command = new ProcessInboundTransactionsCommand(
                    message.SourceName, payload.ExternalAccountId, payload.Institution, payload.Transactions, message.Id.Value,
                    message.Channel ?? AuditChannels.Unknown);

                // Marked first so the handler's commit records it with the rows it writes.
                message.MarkProcessed();

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("Inbox message {MessageId} refused: {ErrorCode}", message.Id.Value, result.Error.Code);
                    await RecordFailureAsync(message, payload, context, $"{result.Error.Code}: {result.Error.Description}",
                        FailureClassifier.Classify(result.Error), stopwatch, cancellationToken);
                    return;
                }

                // Commits the Processed mark when the handler had nothing to write.
                await context.SaveChangesAsync(cancellationToken);

                MessagingTelemetry.ProcessingDuration.WithLabels(Queue, "processed").Observe(stopwatch.Elapsed.TotalSeconds);
                MessagingTelemetry.EndToEndLag.WithLabels(Queue).Observe((DateTime.UtcNow - message.ReceivedAt).TotalSeconds);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var kind = FailureClassifier.Classify(ex);
                _logger.LogError(ex, "Inbox message {MessageId} from {SourceName} failed on attempt {Attempt} ({FailureKind})",
                    message.Id.Value, message.SourceName, message.Attempts, kind);
                activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
                await RecordFailureAsync(message, payload, context, FailureClassifier.Describe(ex), kind, stopwatch, cancellationToken);
            }
        }

        private async Task RecordFailureAsync(
            InboxMessage message,
            InboundTransactionsPayload? payload,
            ITransactionsDbContext context,
            string error,
            FailureKind kind,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            context.DiscardPendingChanges();

            if (kind == FailureKind.Permanent)
                message.MarkDeadLettered(error);
            else
                message.MarkFailed(error, RetryBackoff.After(message.Attempts), _options.MaxAttempts);

            var deadLettered = message.Status == InboxMessageStatus.DeadLettered;
            if (deadLettered)
            {
                DeadLetterMetrics.InboxMessagesDeadLettered.WithLabels(message.SourceName).Inc();
                _logger.LogError("Inbox message {MessageId} from {SourceName} dead-lettered after {Attempts} attempt(s), account {AccountRef}",
                    message.Id.Value, message.SourceName, message.Attempts, LogRedaction.Account(payload?.ExternalAccountId));
            }

            var metadata = new Dictionary<string, string>
            {
                ["attempt"] = message.Attempts.ToString(),
                ["maxAttempts"] = _options.MaxAttempts.ToString(),
                ["failureKind"] = kind.ToString()
            };
            if (message.NextAttemptAt is { } nextAttemptAt && !deadLettered)
                metadata["nextAttemptAt"] = nextAttemptAt.ToString("O");

            context.StageAudit(
            [
                new AuditEventRecord(
                    EventId: Guid.NewGuid(),
                    EventType: deadLettered ? AuditEventTypes.InboundDeadLettered : AuditEventTypes.InboundProcessingFailed,
                    OccurredAt: DateTime.UtcNow,
                    Channel: message.Channel ?? AuditChannels.Unknown,
                    SourceName: message.SourceName,
                    ExternalAccountId: payload?.ExternalAccountId,
                    InboxMessageId: message.Id.Value,
                    IdempotencyKey: message.IdempotencyKey,
                    Detail: error,
                    Metadata: metadata,
                    TraceId: InboundAudit.CurrentTraceId)
            ]);

            await context.SaveChangesAsync(cancellationToken);
            MessagingTelemetry.ProcessingDuration.WithLabels(Queue, "failed").Observe(stopwatch.Elapsed.TotalSeconds);
        }

        private static async Task RecordBacklogAsync(IMessagingDbContext messaging, CancellationToken cancellationToken)
        {
            var pending = await messaging.InboxMessages.CountAsync(m => m.Status == InboxMessageStatus.Pending, cancellationToken);
            var deadLettered = await messaging.InboxMessages.CountAsync(m => m.Status == InboxMessageStatus.DeadLettered, cancellationToken);
            MessagingTelemetry.Backlog.WithLabels(Queue, "pending").Set(pending);
            MessagingTelemetry.Backlog.WithLabels(Queue, "dead_lettered").Set(deadLettered);
        }
    }
}