namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class AggregationOptions
    {
        public const string SectionName = "Aggregation";

        public bool Enabled { get; set; } = true;

        public int IntervalMinutes { get; set; } = 60;

        // Look back this far before the checkpoint for rows that committed late.
        public int OverlapMinutes { get; set; } = 10;
    }
}