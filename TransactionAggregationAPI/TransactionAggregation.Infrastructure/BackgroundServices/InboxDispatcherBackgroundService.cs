using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Inbox;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions;

namespace TransactionAggregation.Infrastructure.BackgroundServices
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
                await ProcessMessageAsync(message, sender, cancellationToken);

            await messaging.SaveChangesAsync(cancellationToken);
        }

        internal async Task ProcessMessageAsync(
            InboxMessage message, ISender sender, CancellationToken cancellationToken)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<InboundTransactionsPayload>(message.Payload)
                    ?? throw new InvalidOperationException(
                        $"Inbox message {message.Id.Value} has an empty/invalid payload");

                var command = new ProcessInboundTransactionsCommand(
                    message.SourceName, payload.ExternalAccountId, payload.Transactions);

                var result = await sender.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    RecordFailure(message, result.Error.Description, ComputeBackoff(message.Attempts), _options.MaxAttempts);
                    return;
                }

                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process inbox message {MessageId} from {SourceName}",
                    message.Id.Value, message.SourceName);
                RecordFailure(message, ex.Message, ComputeBackoff(message.Attempts), _options.MaxAttempts);
            }
        }

        private static void RecordFailure(InboxMessage message, string error, TimeSpan backoff, int maxAttempts)
        {
            message.MarkFailed(error, backoff, maxAttempts);
            if (message.Status == InboxMessageStatus.DeadLettered)
                DeadLetterMetrics.InboxMessagesDeadLettered.WithLabels(message.SourceName).Inc();
        }

        private static TimeSpan ComputeBackoff(int attempts)
        {
            var seconds = Math.Min(Math.Pow(2, attempts + 1), 300);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
