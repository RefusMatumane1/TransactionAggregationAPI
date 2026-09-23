using SharedKernel.Abstractions;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Commands.CategorizeTransaction
{
    public sealed record CategorizeTransactionCommand(Guid TransactionId, TransactionCategory Category) : ICommand;
}