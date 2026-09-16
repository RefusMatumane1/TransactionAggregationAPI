using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    /// <summary>Does the actual work of ingesting a batch of transactions an account-aggregator
    /// pushed to us — resolve the BankLink, dedupe, create + categorize, enqueue sync side
    /// effects. Only ever invoked internally by InboxDispatcherBackgroundService once an
    /// InboxMessage has been claimed; never exposed over HTTP directly, so the payload is
    /// trusted to already be shape-valid (that happened at ReceiveBankTransactionsCommand time,
    /// before it was written to the inbox).</summary>
    public sealed record ProcessInboundTransactionsCommand(
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<int>;
}
