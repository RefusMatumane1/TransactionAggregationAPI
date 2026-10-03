using BuildingBlocks.Application.Abstractions;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    public sealed record ProcessInboundTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    string? Institution,
    IReadOnlyList<ExternalTransactionDTO> Transactions,
    Guid? InboxMessageId = null,
    string? Channel = null) : ICommand<int>;
}