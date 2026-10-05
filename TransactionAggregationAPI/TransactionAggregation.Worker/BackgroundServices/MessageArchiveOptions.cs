namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class MessageArchiveOptions
    {
        public const string SectionName = "MessageArchive";

        public bool Enabled { get; set; } = true;
        public int RetainDays { get; set; } = 30;
        public int BatchSize { get; set; } = 1_000;
        public int MaxBatchesPerRun { get; set; } = 50;
        public int IntervalSeconds { get; set; } = 60;
    }
}