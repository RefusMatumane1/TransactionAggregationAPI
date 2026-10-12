namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class AggregationOptions
    {
        public const string SectionName = "Aggregation";

        public bool Enabled { get; set; } = true;

        public int IntervalMinutes { get; set; } = 5;

        // Look back this far before the checkpoint for rows that committed late.
        public int OverlapMinutes { get; set; } = 10;

        // New entries folded in per refresh; a bigger backlog is worked off in back-to-back runs.
        public int MaxRowsPerRun { get; set; } = 500_000;
    }
}