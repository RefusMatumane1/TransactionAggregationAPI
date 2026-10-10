namespace Modules.Customers.Contracts
{
    public sealed record LinkedAccountRef(string Institution, string ExternalAccountId);

    public interface ICustomerAccounts
    {
        Task<IReadOnlyList<LinkedAccountRef>?> FindAsync(Guid customerId, CancellationToken cancellationToken = default);
    }
}