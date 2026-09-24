namespace Modules.Audit.Contracts
{
    /// <summary>
    /// One audit fact, as producers hand it over. EventId is the idempotency key:
    /// recording the same EventId twice (outbox redelivery, Kafka redelivery) stores it once,
    /// so producers must generate it when the fact happens, not when it's recorded.
    /// </summary>
    /// <param name="Channel">One of <see cref="AuditChannels"/>.</param>
    /// <param name="SourceName">Who sent it: the webhook source name, or the configured Kafka source name.</param>
    /// <param name="InboxMessageId">Correlates every event belonging to one delivery.</param>
    /// <param name="Detail">Short human-readable outcome/reason (rejection reason, error message).</param>
    /// <param name="Metadata">
    /// Channel-specific facts: remote IP / user agent for webhooks, topic / partition / offset for Kafka,
    /// counts and hashes for processing. Never put secrets (API keys, tokens) here.
    /// </param>
    public sealed record AuditEventRecord(
        Guid EventId,
        string EventType,
        DateTime OccurredAt,
        string Channel,
        string SourceName,
        string? ExternalAccountId = null,
        Guid? InboxMessageId = null,
        string? IdempotencyKey = null,
        Guid? CustomerId = null,
        Guid? TransactionId = null,
        string? ExternalTransactionId = null,
        string? Detail = null,
        IReadOnlyDictionary<string, string>? Metadata = null,
        string? TraceId = null);
}