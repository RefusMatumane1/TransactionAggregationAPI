namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class OutboxOptions : DispatcherOptions
    {
        public const string SectionName = "Outbox";

        public int MaxConcurrency { get; set; } = 16;
    }
}