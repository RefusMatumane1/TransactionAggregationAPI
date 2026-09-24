using Modules.Transactions.Domain.Enums;
using SharedKernel.Abstractions;

namespace Modules.Transactions.Application.Features.Transactions.Commands.CategorizeTransaction
{
    /// <summary>
    /// Scoped to <paramref name="CustomerId"/>: ownership is part of the lookup itself, so
    /// another customer's transaction and a missing one run the same query, match no row and
    /// fail at the same point — neither the response nor its timing tells them apart.
    /// </summary>
    public sealed record CategorizeTransactionCommand(Guid TransactionId, Guid CustomerId, TransactionCategory Category) : ICommand;
}