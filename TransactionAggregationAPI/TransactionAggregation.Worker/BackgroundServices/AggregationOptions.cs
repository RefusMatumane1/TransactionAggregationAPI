namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class AggregationOptions
    {
        public const string SectionName = "Aggregation";

        public bool Enabled { get; set; } = true;

        // How often the daily read model is rebuilt; aggregate reads are at most this stale.
        public int IntervalMinutes { get; set; } = 60;

        // How far before the last checkpoint each refresh looks again, for ledger rows whose
        // transaction committed after the previous refresh had read past their CreatedAt.
        public int OverlapMinutes { get; set; } = 10;
    }
}