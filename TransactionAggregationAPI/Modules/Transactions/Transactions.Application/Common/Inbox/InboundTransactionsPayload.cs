using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Common.Inbox
{
    public sealed record InboundTransactionsPayload(
    string ExternalAccountId, string? Institution, IReadOnlyList<ExternalTransactionDTO> Transactions);
}