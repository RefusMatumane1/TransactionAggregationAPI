using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using System.Text.Json;

namespace Modules.Transactions.Infrastructure.BackgroundServices
{
    public sealed class InboxDispatcherBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InboxDispatcherBackgroundService> _logger;
        private readonly InboxOptions _options;

        public InboxDispatcherBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<InboxDispatcherBackgroundService> logger,
            IOptions<InboxOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
            _logger.LogInformation("Inbox dispatcher started. Poll interval: {Interval}", interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error while dispatching inbox messages");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var claimed = await messaging.ClaimInboxMessagesAsync(
                _options.BatchSize, TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes), cancellationToken);
            if (claimed.Count == 0)
                return;

            _logger.LogInformation("Claimed {Count} inbox messages", claimed.Count);

            foreach (var message in claimed)
                await ProcessMessageAsync(message, sender, messaging, cancellationToken);

            // Persists each message's new status together with the audit records of its
            // failures, so a retry/dead-letter is audited if and only if it was recorded.
            await messaging.SaveChangesAsync(cancellationToken);
        }

        internal async Task ProcessMessageAsync(
            InboxMessage message, ISender sender, IMessagingDbContext messaging, CancellationToken cancellationToken)
        {
            InboundTransactionsPayload? payload = null;
            try
            {
                payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)
                    ?? throw new PoisonMessageException(
                        $"Inbox message {message.Id.Value} has an empty/invalid payload");

                var command = new ProcessInboundTransactionsCommand(
                    message.SourceName, payload.ExternalAccountId, payload.Transactions, message.Id.Value,
                    message.Channel ?? AuditChannels.Unknown);

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    RecordFailure(message, payload, messaging, result.Error.Description, FailureClassifier.Classify(result.Error));
                    return;
                }

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                var kind = FailureClassifier.Classify(ex);
                _logger.LogError(ex, "Failed to process inbox message {MessageId} from {SourceName} ({FailureKind})",
                    message.Id.Value, message.SourceName, kind);
                RecordFailure(message, payload, messaging, ex.Message, kind);
            }
        }

        private void RecordFailure(
            InboxMessage message,
            InboundTransactionsPayload? payload,
            IMessagingDbContext messaging,
            string error,
            FailureKind kind)
        {
            var maxAttempts = _options.MaxAttempts;
            if (kind == FailureKind.Permanent)
                message.MarkDeadLettered(error);
            else
                message.MarkFailed(error, ComputeBackoff(message.Attempts), maxAttempts);

            var deadLettered = message.Status == InboxMessageStatus.DeadLettered;
            if (deadLettered)
                DeadLetterMetrics.InboxMessagesDeadLettered.WithLabels(message.SourceName).Inc();

            var metadata = new Dictionary<string, string>
            {
                ["attempt"] = message.Attempts.ToString(),
                ["maxAttempts"] = maxAttempts.ToString(),
                ["failureKind"] = kind.ToString()
            };
            if (message.NextAttemptAt is { } nextAttemptAt && !deadLettered)
                metadata["nextAttemptAt"] = nextAttemptAt.ToString("O");

            InboundAudit.Enqueue(messaging,
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
        }

        private static TimeSpan ComputeBackoff(int attempts)
        {
            var seconds = Math.Min(Math.Pow(2, attempts + 1), 300);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}