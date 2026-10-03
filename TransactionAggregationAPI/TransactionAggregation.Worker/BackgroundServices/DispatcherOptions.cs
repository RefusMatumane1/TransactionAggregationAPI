namespace TransactionAggregation.Worker.BackgroundServices
{
    public abstract class DispatcherOptions
    {
        public int PollIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 50;
        public int MaxAttempts { get; set; } = 5;

        public int ClaimTimeoutMinutes { get; set; } = 10;
    }
}