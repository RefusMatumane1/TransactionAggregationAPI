using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Common.Inbox
{
    // Institution is what the delivery named, if anything; the bank is always the source.
    public sealed record InboundTransactionsPayload(
    string ExternalAccountId, string? Institution, IReadOnlyList<ExternalTransactionDTO> Transactions);
}