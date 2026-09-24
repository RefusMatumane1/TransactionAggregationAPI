using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Abstractions;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions
{
    /// <param name="InboxMessageId">The delivery being processed — correlates every audit event it produces.</param>
    /// <param name="Channel">The channel the delivery arrived on (from the inbox row), for the audit trail.</param>
    public sealed record ProcessInboundTransactionsCommand(
    string SourceName,
    string ExternalAccountId,
    IReadOnlyList<ExternalTransactionDTO> Transactions,
    Guid? InboxMessageId = null,
    string? Channel = null) : ICommand<int>;
}