using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TransactionAggregation.MockAggregator.Banks;
using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Feed
{
    public sealed class FeedBackgroundService(
        TransactionGenerator generator,
        IDeliveryChannel channel,
        IOptions<FeedOptions> options,
        ILogger<FeedBackgroundService> logger) : BackgroundService
    {
        private readonly ConcurrentDictionary<string, DeliveryBatch> _lastBatch = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!options.Value.Enabled)
            {
                logger.LogInformation("Mock bank feed is disabled (Feed:Enabled=false)");
                return;
            }

            logger.LogInformation("Mock bank feed delivering every {Interval}s over {Channel}",
                options.Value.IntervalSeconds, options.Value.Channel);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.IntervalSeconds));
            do
            {
                foreach (var account in MockCatalog.Accounts)
                {
                    try
                    {
                        await TickAsync(account, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Delivery for {Account} failed — continuing with the next tick", account.Id);
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task TickAsync(MockAccount account, CancellationToken cancellationToken)
        {
            if (_lastBatch.TryGetValue(account.Id, out var previous) && Random.Shared.NextDouble() < options.Value.RedeliveryShare)
            {
                logger.LogInformation("Re-sending the previous batch for {Account} (simulated redelivery)", account.Id);
                await channel.DeliverAsync(previous, cancellationToken);
            }

            var transactions = generator.NextBatch(account);
            if (transactions.Count == 0)
                return;

            var style = BankStatementStyles.ByInstitution[account.Institution];
            var batch = new DeliveryBatch(
                account,
                IdempotencyKey: Guid.NewGuid().ToString("N"),
                new DeliveryPayload(account.Id, account.Institution, transactions.Select(style.Render).ToList()));

            await channel.DeliverAsync(batch, cancellationToken);
            _lastBatch[account.Id] = batch;
        }
    }
}