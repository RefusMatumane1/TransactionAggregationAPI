using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.Interfaces;
using System.Text.Json;

namespace Modules.Transactions.Infrastructure.BackgroundServices
{
    public sealed class OutboxDispatcherBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxDispatcherBackgroundService> _logger;
        private readonly OutboxOptions _options;

        public OutboxDispatcherBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxDispatcherBackgroundService> logger,
            IOptions<OutboxOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromSeconds(_options.PollIntervalSeconds);
            _logger.LogInformation("Outbox dispatcher started. Poll interval: {Interval}", interval);

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
                    _logger.LogError(ex, "Unhandled error while dispatching outbox messages");
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
            var context = scope.ServiceProvider.GetRequiredService<ITransactionsDbContext>();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var auditTrail = scope.ServiceProvider.GetRequiredService<IAuditTrail>();

            var claimed = await messaging.ClaimOutboxMessagesAsync(
                _options.BatchSize, TimeSpan.FromMinutes(_options.ClaimTimeoutMinutes), cancellationToken);
            if (claimed.Count == 0)
                return;

            _logger.LogInformation("Claimed {Count} outbox messages", claimed.Count);

            foreach (var message in claimed)
                await ProcessMessageAsync(message, context, cache, analytics, notifications, auditTrail, cancellationToken);

            await messaging.SaveChangesAsync(cancellationToken);
        }

        internal async Task ProcessMessageAsync(
            OutboxMessage message,
            ITransactionsDbContext context,
            ICacheService cache,
            IAnalyticsService analytics,
            INotificationService notifications,
            IAuditTrail auditTrail,
            CancellationToken cancellationToken)
        {
            try
            {
                if (OutboxSchemaVersions.IsNewerThanReadable(message.Type, message.SchemaVersion))
                {
                    _logger.LogWarning(
                        "Outbox message {MessageId} ({Type}) has schema version {SchemaVersion}, newer than this dispatcher reads — dead-lettering",
                        message.Id.Value, message.Type, message.SchemaVersion);
                    RecordFailure(message, $"Unsupported schema version {message.SchemaVersion} for {message.Type}", FailureKind.Permanent);
                    return;
                }

                switch (message.Type)
                {
                    case OutboxMessageTypes.TransactionCreated:
                        await HandleTransactionCreatedAsync(message, context, analytics, notifications, cancellationToken);
                        break;

                    case OutboxMessageTypes.TransactionCategorized:
                        await HandleTransactionCategorizedAsync(message, context, cache, analytics, notifications, cancellationToken);
                        break;

                    case OutboxMessageTypes.TransactionSynced:
                        await HandleTransactionSyncedAsync(message, context, cache, analytics, cancellationToken);
                        break;

                    case OutboxMessageTypes.TransactionsExpired:
                        foreach (var customerId in Deserialize<TransactionsExpiredOutboxPayload>(message).CustomerIds)
                        {
                            await cache.RemoveByPatternAsync($"transactions:{customerId}*", cancellationToken);
                            await cache.RemoveByPatternAsync($"summary:{customerId}*", cancellationToken);
                        }
                        break;

                    case OutboxMessageTypes.DuplicateInboundDetected:
                        await notifications.SendDuplicateInboundAlertAsync(
                            Deserialize<DuplicateInboundDetectedOutboxPayload>(message), cancellationToken);
                        break;

                    // Idempotent by EventId, so a redelivered outbox message (crash after
                    // recording, before MarkProcessed was saved) doesn't duplicate audit rows.
                    case AuditOutbox.MessageType:
                        await auditTrail.RecordAsync(
                            Deserialize<AuditOutboxPayload>(message).Events, cancellationToken);
                        break;

                    default:
                        _logger.LogWarning(
                            "Unknown outbox message type {Type} for message {MessageId} — dead-lettering",
                            message.Type, message.Id.Value);
                        RecordFailure(message, $"Unknown message type: {message.Type}", FailureKind.Permanent);
                        return;
                }

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                var kind = FailureClassifier.Classify(ex);
                _logger.LogError(ex, "Failed to process outbox message {MessageId} ({Type}, {FailureKind})",
                    message.Id.Value, message.Type, kind);
                RecordFailure(message, ex.Message, kind);
            }
        }

        private void RecordFailure(OutboxMessage message, string error, FailureKind kind)
        {
            if (kind == FailureKind.Permanent)
                message.MarkDeadLettered(error);
            else
                message.MarkFailed(error, ComputeBackoff(message.Attempts), _options.MaxAttempts);

            if (message.Status == OutboxMessageStatus.DeadLettered)
                DeadLetterMetrics.OutboxMessagesDeadLettered.WithLabels(message.Type).Inc();
        }

        private static async Task HandleTransactionCreatedAsync(
            OutboxMessage message,
            ITransactionsDbContext context,
            IAnalyticsService analytics,
            INotificationService notifications,
            CancellationToken cancellationToken)
        {
            var payload = Deserialize<TransactionCreatedOutboxPayload>(message);
            var transaction = await GetTransactionAsync(context, payload.TransactionId, cancellationToken);
            if (transaction is null)
                return;

            await analytics.TrackTransactionCreatedAsync(transaction, cancellationToken);
            await notifications.SendTransactionNotificationAsync(
                transaction, NotificationType.TransactionCreated, cancellationToken);
        }

        private static async Task HandleTransactionCategorizedAsync(
            OutboxMessage message,
            ITransactionsDbContext context,
            ICacheService cache,
            IAnalyticsService analytics,
            INotificationService notifications,
            CancellationToken cancellationToken)
        {
            var payload = Deserialize<TransactionCategorizedOutboxPayload>(message);

            await cache.RemoveByPatternAsync($"transactions:{payload.CustomerId}*", cancellationToken);
            await cache.RemoveByPatternAsync($"summary:{payload.CustomerId}*", cancellationToken);

            var transaction = await GetTransactionAsync(context, payload.TransactionId, cancellationToken);
            if (transaction is null)
                return;

            await analytics.TrackTransactionCategorizedAsync(
                transaction, payload.OldCategory, payload.NewCategory, payload.IsAutoCategorized, cancellationToken);

            if (!payload.IsAutoCategorized)
            {
                await notifications.SendTransactionNotificationAsync(
                    transaction, NotificationType.TransactionCategorized, cancellationToken);
            }
        }

        private static async Task HandleTransactionSyncedAsync(
            OutboxMessage message,
            ITransactionsDbContext context,
            ICacheService cache,
            IAnalyticsService analytics,
            CancellationToken cancellationToken)
        {
            var payload = Deserialize<TransactionSyncedOutboxPayload>(message);

            await cache.RemoveByPatternAsync($"transactions:{payload.CustomerId}*", cancellationToken);
            await cache.RemoveByPatternAsync($"summary:{payload.CustomerId}*", cancellationToken);

            var transaction = await GetTransactionAsync(context, payload.TransactionId, cancellationToken);
            if (transaction is null)
                return;

            await analytics.TrackTransactionSyncedAsync(transaction, cancellationToken);
        }

        private static Task<Transaction?> GetTransactionAsync(
            ITransactionsDbContext context, Guid transactionId, CancellationToken cancellationToken) =>
            context.Transactions.FirstOrDefaultAsync(t => t.Id == TransactionId.CreateFrom(transactionId), cancellationToken);

        private static T Deserialize<T>(OutboxMessage message) =>
            JsonSerializer.Deserialize<T>(message.Payload)
                ?? throw new PoisonMessageException(
                    $"Outbox message {message.Id.Value} ({message.Type}) has an empty/invalid payload");

        private static TimeSpan ComputeBackoff(int attempts)
        {
            var seconds = Math.Min(Math.Pow(2, attempts + 1), 300);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}