namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class AggregationOptions
    {
        public const string SectionName = "Aggregation";

        public bool Enabled { get; set; } = true;

        public int IntervalMinutes { get; set; } = 5;

        public int OverlapMinutes { get; set; } = 10;

        public int MaxRowsPerRun { get; set; } = 500_000;
    }
}