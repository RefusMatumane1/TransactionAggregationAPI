using SharedKernel.Abstractions;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    public sealed record ReceiveBankTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<Guid>;
}