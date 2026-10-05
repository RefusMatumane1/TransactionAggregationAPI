using Contract = Modules.Transactions.Contracts.IntegrationEvents;

namespace Modules.Transactions.Application.Common.Outbox
{
    public static class OutboxMessageTypes
    {
        // Published to the broker for other systems (Transactions.Contracts).
        public const string TransactionRecorded = Contract.TransactionRecorded.EventType;

        // Internal: raises the duplicate-delivery alert.
        public const string DuplicateInboundDetected = "DuplicateInboundDetected";
    }

    public sealed record DuplicateInboundDetectedOutboxPayload(
        string Level,
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<string> DuplicateExternalIds,
        Guid? InboxMessageId,
        DateTime DetectedAt);
}