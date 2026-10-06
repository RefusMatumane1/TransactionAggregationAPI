using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Prometheus;
using System.Diagnostics;

namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class DailyTotalsRefreshBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AggregationOptions> options,
        ILogger<DailyTotalsRefreshBackgroundService> logger) : PollingBackgroundService(logger)
    {
        internal static readonly Gauge LastRefreshCompleted = Metrics.CreateGauge(
            "transaction_aggregates_last_refresh_timestamp_seconds",
            "Unix time the daily read model last finished a refresh; aggregate reads are as old as this.");

        internal static readonly Histogram RefreshDuration = Metrics.CreateHistogram(
            "transaction_aggregates_refresh_duration_seconds",
            "Time taken by one refresh of the daily read model.",
            new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(0.05, 2, 14) });

        internal static readonly Counter AccountDaysRecomputed = Metrics.CreateCounter(
            "transaction_aggregates_account_days_recomputed_total",
            "Account-days of the daily read model recomputed from the ledger.");

        private readonly AggregationOptions _options = options.Value;

        protected override TimeSpan Interval => TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes));

        protected override TimeSpan InitialDelay => TimeSpan.FromSeconds(15);

        internal static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(30);

        // Failed refreshes retry after 30 s, 1 min, 2 min... capped at the interval.
        protected override TimeSpan DelayAfterFailure(int consecutiveFailures) => RetryDelay(consecutiveFailures, Interval);

        internal static TimeSpan RetryDelay(int consecutiveFailures, TimeSpan interval)
        {
            var backoff = FirstRetry * Math.Pow(2, Math.Clamp(consecutiveFailures - 1, 0, 16));
            return backoff < interval ? backoff : interval;
        }

        protected override async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();
            var refresher = scope.ServiceProvider.GetRequiredService<IDailyTotalsRefresher>();

            var stopwatch = Stopwatch.StartNew();
            var refresh = await refresher.RefreshAsync(TimeSpan.FromMinutes(Math.Max(0, _options.OverlapMinutes)), cancellationToken);
            if (refresh is null)
            {
                logger.LogInformation("Daily totals refresh skipped: another replica holds the refresh lock");
                return false;
            }

            RefreshDuration.Observe(stopwatch.Elapsed.TotalSeconds);
            AccountDaysRecomputed.Inc(refresh.AccountDaysRecomputed);
            LastRefreshCompleted.SetToCurrentTimeUtc();
            logger.LogInformation("Daily totals refreshed as of {AsOf:O}: {AccountDays} account-days recomputed in {ElapsedMs} ms",
                refresh.AsOf, refresh.AccountDaysRecomputed, stopwatch.ElapsedMilliseconds);

            await InvalidateCachedAggregatesAsync(scope.ServiceProvider.GetRequiredService<ICacheService>(), cancellationToken);
            return false;
        }

        // Totals are committed; a cache failure only delays fresh totals until entries expire.
        private async Task InvalidateCachedAggregatesAsync(ICacheService cache, CancellationToken cancellationToken)
        {
            try
            {
                await cache.InvalidateScopeAsync(TransactionCacheScopes.Aggregates, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Daily totals refreshed but the cached aggregates could not be invalidated; they will expire on their own");
            }
        }
    }
}