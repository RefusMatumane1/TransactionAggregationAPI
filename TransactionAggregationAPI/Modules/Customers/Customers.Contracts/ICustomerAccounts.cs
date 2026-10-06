namespace Modules.Customers.Contracts
{
    // A bank account linked to a customer, as transactions identify it.
    public sealed record LinkedAccountRef(string Institution, string ExternalAccountId);

    // How other modules turn "customer X" into the accounts whose transactions are theirs.
    public interface ICustomerAccounts
    {
        // Null when the customer does not exist; an empty list when it has no linked accounts.
        Task<IReadOnlyList<LinkedAccountRef>?> FindAsync(Guid customerId, CancellationToken cancellationToken = default);
    }
}