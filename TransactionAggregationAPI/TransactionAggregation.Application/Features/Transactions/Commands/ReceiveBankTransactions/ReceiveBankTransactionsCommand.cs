using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed record ReceiveBankTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<Guid>;
}