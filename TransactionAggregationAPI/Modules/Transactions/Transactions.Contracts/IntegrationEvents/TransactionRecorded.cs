namespace Modules.Transactions.Contracts.IntegrationEvents
{
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