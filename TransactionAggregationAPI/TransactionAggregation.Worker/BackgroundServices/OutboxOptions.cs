namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class OutboxOptions : DispatcherOptions
    {
        public const string SectionName = "Outbox";

        // Messages of one claimed batch published at the same time. Handlers do no database work,
        // so this bounds only broker and webhook load.
        public int MaxConcurrency { get; set; } = 16;
    }
}