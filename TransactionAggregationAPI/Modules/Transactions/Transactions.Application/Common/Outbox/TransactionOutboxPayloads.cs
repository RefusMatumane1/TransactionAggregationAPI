using Contract = Modules.Transactions.Contracts.IntegrationEvents;

namespace Modules.Transactions.Application.Common.Outbox
{
    public static class OutboxMessageTypes
    {
        public const string TransactionRecorded = Contract.TransactionRecorded.EventType;

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