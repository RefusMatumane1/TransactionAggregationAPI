using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Common.Outbox;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Outbox;

namespace TransactionAggregation.Infrastructure.BackgroundServices
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
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var claimed = await context.ClaimOutboxMessagesAsync(_options.BatchSize, cancellationToken);
            if (claimed.Count == 0)
                return;

            _logger.LogInformation("Claimed {Count} outbox messages", claimed.Count);

            foreach (var message in claimed)
                await ProcessMessageAsync(message, context, cache, analytics, notifications, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task ProcessMessageAsync(
            OutboxMessage message,
            IApplicationDbContext context,
            ICacheService cache,
            IAnalyticsService analytics,
            INotificationService notifications,
            CancellationToken cancellationToken)
        {
            try
            {
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

                    default:
                        _logger.LogWarning(
                            "Unknown outbox message type {Type} for message {MessageId} — dead-lettering",
                            message.Type, message.Id.Value);
                        message.MarkFailed($"Unknown message type: {message.Type}", TimeSpan.Zero, maxAttempts: 1);
                        return;
                }

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId} ({Type})", message.Id.Value, message.Type);
                message.MarkFailed(ex.Message, ComputeBackoff(message.Attempts), _options.MaxAttempts);
            }
        }

        private static async Task HandleTransactionCreatedAsync(
            OutboxMessage message,
            IApplicationDbContext context,
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
            IApplicationDbContext context,
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
                    transaction, NotificationType.TransactionCreated, cancellationToken);
            }
        }

        private static async Task HandleTransactionSyncedAsync(
            OutboxMessage message,
            IApplicationDbContext context,
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
            IApplicationDbContext context, Guid transactionId, CancellationToken cancellationToken) =>
            context.Transactions.FirstOrDefaultAsync(t => t.Id == TransactionId.CreateFrom(transactionId), cancellationToken);

        private static T Deserialize<T>(OutboxMessage message) =>
            JsonSerializer.Deserialize<T>(message.Payload)
                ?? throw new InvalidOperationException(
                    $"Outbox message {message.Id.Value} ({message.Type}) has an empty/invalid payload");

        private static TimeSpan ComputeBackoff(int attempts)
        {
            var seconds = Math.Min(Math.Pow(2, attempts + 1), 300);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}