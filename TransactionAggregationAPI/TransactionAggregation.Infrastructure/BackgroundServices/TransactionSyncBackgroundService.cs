using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Infrastructure.BackgroundServices
{
    /// <summary>
    /// Hosted service that periodically pulls transactions from all configured external sources
    /// for every customer and persists new records. Runs on a configurable interval.
    /// </summary>
    public sealed class TransactionSyncBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TransactionSyncBackgroundService> _logger;
        private readonly TimeSpan _interval;

        public TransactionSyncBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<TransactionSyncBackgroundService> logger,
            IOptions<TransactionSyncOptions> options)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(options.Value.IntervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Transaction sync background service started. Interval: {Interval}",
                _interval);

            // Stagger the first run slightly to allow app startup to complete
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await SyncAllCustomersAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task SyncAllCustomersAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting scheduled transaction sync for all customers");

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var aggregator = scope.ServiceProvider.GetRequiredService<ITransactionAggregator>();

                // Fetch minimal customer data; materialize before extracting the value-object Guid
                var customers = await context.Customers
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var customerIds = customers.Select(c => c.Id.Value).ToList();

                _logger.LogInformation("Syncing transactions for {Count} customers", customerIds.Count);

                var fromDate = DateTime.UtcNow.AddDays(-7);
                var toDate = DateTime.UtcNow;

                foreach (var customerId in customerIds)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    await SyncCustomerAsync(context, aggregator, customerId, fromDate, toDate, cancellationToken);
                }

                _logger.LogInformation("Scheduled transaction sync completed for {Count} customers", customerIds.Count);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Transaction sync was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during scheduled transaction sync");
            }
        }

        private async Task SyncCustomerAsync(
            IApplicationDbContext context,
            ITransactionAggregator aggregator,
            Guid customerId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var existingExternalIds = await context.Transactions
                    .Where(t => t.CustomerId == Domain.Common.ValueObjects.CustomerId.CreateFrom(customerId))
                    .Select(t => t.Source.ExternalId)
                    .ToHashSetAsync(cancellationToken);

                var aggregationResult = await aggregator.AggregateCustomerTransactionsAsync(
                    customerId, fromDate, toDate, cancellationToken);

                var newTransactions = aggregationResult.Transactions
                    .Where(t => !existingExternalIds.Contains(t.Source.ExternalId))
                    .ToList();

                if (newTransactions.Count > 0)
                {
                    await context.Transactions.AddRangeAsync(newTransactions, cancellationToken);
                    await context.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Synced {NewCount} new transactions for customer {CustomerId}",
                        newTransactions.Count, customerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to sync transactions for customer {CustomerId}", customerId);
            }
        }
    }

    public sealed class TransactionSyncOptions
    {
        public const string SectionName = "TransactionSync";
        public int IntervalMinutes { get; set; } = 60;
    }
}
