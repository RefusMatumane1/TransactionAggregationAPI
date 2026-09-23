using SharedKernel.Abstractions;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    public sealed record ProcessInboundTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions) : ICommand<int>;
}