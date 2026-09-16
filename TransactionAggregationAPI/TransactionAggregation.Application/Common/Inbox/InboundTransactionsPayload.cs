using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Common.Inbox
{
    public sealed record InboundTransactionsPayload(
    string ExternalAccountId, IReadOnlyList<ExternalTransactionDTO> Transactions);
}