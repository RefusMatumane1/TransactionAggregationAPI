namespace TransactionAggregation.Worker.BackgroundServices
{
    public abstract class PollingBackgroundService(ILogger logger) : BackgroundService
    {
        protected abstract TimeSpan Interval { get; }

        protected virtual TimeSpan InitialDelay => TimeSpan.Zero;

        // Returns true when the run found a full batch, so more work is likely waiting: the next run
        // starts at once instead of after Interval. A failed run always waits.
        protected abstract Task<bool> RunOnceAsync(CancellationToken cancellationToken);

        protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("{Job} started. Poll interval: {Interval}", GetType().Name, Interval);

            if (!await DelayAsync(InitialDelay, stoppingToken))
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (await RunOnceAsync(stoppingToken))
                        continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Job} run failed; retrying next interval", GetType().Name);
                }

                if (!await DelayAsync(Interval, stoppingToken))
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