namespace TransactionAggregation.Application.Common.Options
{
    public class InboxOptions
    {
        public const string SectionName = "Inbox";

        public int PollIntervalSeconds { get; set; } = 5;
        public int BatchSize { get; set; } = 50;
        public int MaxAttempts { get; set; } = 5;
    }
}
