using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Abstractions;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    /// <summary>
    /// The single entry point for every inbound channel (REST webhook, Kafka): each channel
    /// only translates its wire format into this command, so the inbox write and its
    /// idempotency rules are identical whichever channel a delivery arrived on.
    /// IdempotencyKey is optional — without one, a hash of the payload is used, so a
    /// byte-for-byte replay is still recognised.
    /// </summary>
    public sealed record ReceiveBankTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions,
    string? IdempotencyKey = null,
    InboundDelivery? Delivery = null,
    int SchemaVersion = BankTransactionsMessage.CurrentSchemaVersion) : ICommand<InboxReceipt>;

    /// <summary>
    /// Where a delivery came from, for the audit trail: the channel plus channel-specific
    /// facts (remote IP / user agent for webhooks, topic / partition / offset for Kafka).
    /// Never put credentials in Metadata.
    /// </summary>
    public sealed record InboundDelivery(string Channel, IReadOnlyDictionary<string, string> Metadata)
    {
        public static InboundDelivery Unspecified { get; } = new(AuditChannels.Unknown, new Dictionary<string, string>());
    }

    /// <param name="Requeued">
    /// The duplicate matched a dead-lettered delivery, which has been given a fresh retry budget.
    /// </param>
    public sealed record InboxReceipt(Guid InboxMessageId, bool IsDuplicate, bool Requeued = false);
}