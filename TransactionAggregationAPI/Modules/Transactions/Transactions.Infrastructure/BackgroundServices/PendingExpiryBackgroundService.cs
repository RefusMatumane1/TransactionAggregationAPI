using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Features.Transactions.Commands.ExpireStalePendingTransactions;
using Prometheus;

namespace Modules.Transactions.Infrastructure.BackgroundServices
{
    /// <summary>
    /// Expires transactions left Pending past PendingExpiry:ExpireAfterDays — authorisations
    /// the bank dropped and will never post, which would otherwise sit in every customer's
    /// pending figures and reduce their available balance forever.
    ///
    /// Safe with several API replicas: each batch commits atomically, and the transaction
    /// row's concurrency token (xmin) makes a replica that loses the race — to another
    /// replica, or to ingestion settling the same row — roll its whole batch back rather
    /// than double-expire or overwrite a real posting. Those rows are re-evaluated next run.
    /// </summary>
    public sealed class PendingExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PendingExpiryOptions> options,
        ILogger<PendingExpiryBackgroundService> logger) : BackgroundService
    {
        internal static readonly Counter TransactionsExpired = Metrics.CreateCounter(
            "pending_transactions_expired_total",
            "Pending transactions expired because the bank never posted them within the configured window.");

        private readonly PendingExpiryOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CheckIntervalMinutes));
            logger.LogInformation(
                "Pending-expiry job started: expiring after {Days} days, checking every {Interval}",
                _options.ExpireAfterDays, interval);

            // Don't compete with startup (migrations, seeding, the first inbox drain).
            if (!await DelayAsync(TimeSpan.FromMinutes(1), stoppingToken))
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Pending-expiry run failed");
                }

                if (!await DelayAsync(interval, stoppingToken))
                    return;
            }
        }

        /// <summary>Expires batches until one comes back short (or fails), so a backlog drains in one run.</summary>
        internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.ExpireAfterDays);
            var batchSize = Math.Max(1, _options.BatchSize);
            var total = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                var result = await sender.Send(new ExpireStalePendingTransactionsCommand(cutoff, batchSize), cancellationToken);
                if (result.IsFailure)
                    break;

                total += result.Value;
                TransactionsExpired.Inc(result.Value);

                if (result.Value < batchSize)
                    break;
            }

            return total;
        }

        private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}