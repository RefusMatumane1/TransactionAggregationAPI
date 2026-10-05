using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Prometheus;
using System.Diagnostics;

namespace TransactionAggregation.Worker.BackgroundServices
{
    // Rebuilds the daily read model on a schedule, so no aggregate is computed on the request path.
    // Only one replica refreshes at a time (the refresher takes an advisory lock); the others skip.
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

        // Soon after start, so a fresh deployment does not serve empty or hour-old totals.
        protected override TimeSpan InitialDelay => TimeSpan.FromSeconds(15);

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

        // The totals are committed either way; if the cache cannot be cleared, entries expire on
        // their own (Caching:ScopeExpirationMinutes), so a cache outage only delays fresh totals.
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