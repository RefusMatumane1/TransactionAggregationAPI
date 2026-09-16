using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    public sealed record ProcessInboundTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<int>;
}