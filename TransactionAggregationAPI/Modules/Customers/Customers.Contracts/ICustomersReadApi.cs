namespace Modules.Customers.Contracts
{
    /// <summary>Read contract for other modules: returns DTOs, never the Customer entity or its DbSet.</summary>
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