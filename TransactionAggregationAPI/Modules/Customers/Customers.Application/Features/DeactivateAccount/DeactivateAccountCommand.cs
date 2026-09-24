using SharedKernel.Abstractions;

namespace Modules.Customers.Application.Features.DeactivateAccount
{
    /// <summary>
    /// Scoped to <paramref name="CustomerId"/>: ownership is part of the lookup itself, so
    /// another customer's account and a missing one run the same query, match no row and
    /// fail at the same point — neither the response nor its timing tells them apart.
    /// </summary>
    public sealed record DeactivateAccountCommand(Guid AccountId, Guid CustomerId) : ICommand;
}