namespace Modules.Transactions.Contracts.IntegrationEvents
{
    // Self-contained; published at least once, so consumers deduplicate on EventId or TransactionId.
    // Additive fields keep version 1.
    public sealed record TransactionRecorded(
        Guid EventId,
        Guid TransactionId,
        string Institution,
        string ExternalAccountId,
        string ExternalTransactionId,
        decimal Amount,
        string Currency,
        string Description,
        string Category,
        DateTime BookedAt,
        DateTime RecordedAt)
    {
        public const string EventType = "TransactionRecorded";
        public const int SchemaVersion = 1;
    }
}