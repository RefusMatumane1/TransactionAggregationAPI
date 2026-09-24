namespace Modules.Customers.Contracts
{
    /// <summary>
    /// Published read contract for other modules — deliberately narrower
    /// than exposing ICustomersDbContext's DbSet (which stays internal to this module).
    /// Returns primitives/DTOs, never the Customer entity, so consumers can't reach
    /// through into this module's internals the way the old shared ITransactionsDbContext
    /// let handlers reach into other modules' DbSets. Used by the Transactions module
    /// (GetCustomerWithTransactionsQueryHandler) to resolve Customer facts
    /// without a cross-schema join.
    /// </summary>
    public interface ICustomersReadApi
    {
        Task<CustomerSummary?> FindCustomerByIdAsync(
            Guid customerId,
            CancellationToken cancellationToken = default);
    }

    public sealed record CustomerSummary(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt);
}