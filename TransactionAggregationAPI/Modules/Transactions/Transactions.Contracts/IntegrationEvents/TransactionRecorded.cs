namespace Modules.Transactions.Contracts.IntegrationEvents
{
    // A posted transaction entered the ledger. Self-contained: a consumer needs nothing else to act
    // on it. Published at least once; consumers deduplicate on EventId (equal to the outbox message
    // id) or on TransactionId. Schema evolution: additive fields keep
    // version 1, anything else is a new version.
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