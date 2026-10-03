using BuildingBlocks.Application.Abstractions;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed record ReceiveBankTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    string? Institution,
    IReadOnlyList<ExternalTransactionDTO> Transactions,
    string? IdempotencyKey = null,
    InboundDelivery? Delivery = null,
    int SchemaVersion = BankTransactionsMessage.CurrentSchemaVersion) : ICommand<InboxReceipt>;

    public sealed record InboundDelivery(string Channel, IReadOnlyDictionary<string, string> Metadata, string? CorrelationId = null)
    {
        public static InboundDelivery Unspecified { get; } = new(AuditChannels.Unknown, new Dictionary<string, string>());
    }

    public sealed record InboxReceipt(Guid InboxMessageId, bool IsDuplicate, bool Requeued = false);
}