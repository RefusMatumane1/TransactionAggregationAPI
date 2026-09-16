using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    /// <summary>Accepts a batch of transactions an account-aggregator pushed to us for one
    /// linked account — see WebhookEndpoints. Only writes an InboxMessage row and acknowledges;
    /// the actual ingestion (BankLink resolution, dedup, categorization, persistence) happens
    /// asynchronously — see ProcessInboundTransactionsCommand and
    /// InboxDispatcherBackgroundService. Returns the new InboxMessage's id, not a persisted
    /// count, since nothing has been processed yet by the time this returns.</summary>
    /// <param name="SourceName">Which entry in BankAggregatorOptions.WebhookSources the caller
    /// authenticated as (set by ApiKeyEndpointFilter) — carried through purely for
    /// logging/audit provenance, not used to authorize anything here.</param>
    public sealed record ReceiveBankTransactionsCommand(
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<Guid>;
}
