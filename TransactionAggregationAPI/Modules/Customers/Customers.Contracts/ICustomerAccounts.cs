using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;

namespace Modules.Customers.Contracts
{
    public sealed record LinkedAccountRef(string Institution, string ExternalAccountId);

    public interface ICustomerAccounts
    {
        /// <summary>Every account linked to the customer, or null when there is no such customer.</summary>
        Task<IReadOnlyList<LinkedAccountRef>?> FindAsync(Guid customerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The customer's accounts at banks <paramref name="access"/> allows, in a stable order. Null when there is no such
        /// customer, or when a restricted caller can read none of their banks — which must look the same as a missing customer.
        /// </summary>
        Task<IReadOnlyList<AccountKey>?> FindVisibleAsync(
            Guid customerId, InstitutionAccess access, CancellationToken cancellationToken = default);
    }
}
