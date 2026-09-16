using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Common.Inbox
{
    /// <summary>The raw webhook request, captured verbatim into an InboxMessage row at receipt
    /// time — see ReceiveBankTransactionsCommandHandler and InboxDispatcherBackgroundService.</summary>
    public sealed record InboundTransactionsPayload(
        string ExternalAccountId, IReadOnlyList<ExternalTransactionDTO> Transactions);
}
