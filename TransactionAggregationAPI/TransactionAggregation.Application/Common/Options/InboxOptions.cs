namespace TransactionAggregation.Application.Common.Options
{
    public class InboxOptions
    {
        public const string SectionName = "Inbox";

        public int PollIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 50;
        public int MaxAttempts { get; set; } = 5;

        /// <summary>
        /// How long a message may sit in Processing before another dispatcher instance
        /// is allowed to reclaim it. Must comfortably exceed the time a normal batch
        /// takes to process — too short risks two dispatchers processing the same
        /// message concurrently after a slow-but-still-alive run; too long delays
        /// recovery from a genuine crash.
        /// </summary>
        public int ClaimTimeoutMinutes { get; set; } = 10;
    }
}