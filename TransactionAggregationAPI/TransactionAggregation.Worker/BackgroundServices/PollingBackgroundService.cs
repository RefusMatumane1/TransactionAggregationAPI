using Prometheus;

namespace TransactionAggregation.Worker.BackgroundServices
{
    public abstract class PollingBackgroundService(ILogger logger) : BackgroundService
    {
        // A job failing run after run is otherwise visible only in logs; liveness stays green.
        internal static readonly Counter FailedRuns = Metrics.CreateCounter(
            "background_job_failed_runs_total",
            "Runs of a polling background job that threw, by job.",
            new CounterConfiguration { LabelNames = ["job"] });

        private int _consecutiveFailures;

        protected abstract TimeSpan Interval { get; }

        protected virtual TimeSpan InitialDelay => TimeSpan.Zero;

        protected virtual TimeSpan DelayAfterFailure(int consecutiveFailures) => Interval;

        // True on a full batch: the next run starts at once.
        protected abstract Task<bool> RunOnceAsync(CancellationToken cancellationToken);

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("{Job} started. Poll interval: {Interval}", GetType().Name, Interval);

            if (!await DelayAsync(InitialDelay, stoppingToken))
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = Interval;
                try
                {
                    var more = await RunOnceAsync(stoppingToken);
                    _consecutiveFailures = 0;
                    if (more)
                        continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _consecutiveFailures++;
                    delay = DelayAfterFailure(_consecutiveFailures);
                    FailedRuns.WithLabels(GetType().Name).Inc();
                    logger.LogError(ex, "{Job} run failed ({ConsecutiveFailures} in a row); retrying in {RetryDelay}",
                        GetType().Name, _consecutiveFailures, delay);
                }

                if (!await DelayAsync(delay, stoppingToken))
                    return;
            }
        }

        private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
        {
            if (delay <= TimeSpan.Zero)
                return !stoppingToken.IsCancellationRequested;

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