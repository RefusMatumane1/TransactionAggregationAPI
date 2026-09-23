namespace Modules.Customers.Contracts
{
    /// <summary>
    /// Owned by Customers: Account.Balance is never stored (see GetAccountByIdQueryHandler's
    /// original comment) — it's always the sum of the account's linked Transactions. But
    /// Transactions hasn't been extracted into its own module yet, so Customers can't
    /// reach it directly without violating module isolation. The implementation
    /// (TransactionBalanceProvider, in the legacy Modules.Transactions.Application,
    /// wired in Program.cs) is what actually sums the transactions — Customers itself has
    /// no dependency on the Transaction entity or its persistence. Mirrors
    /// IAccountProvisioningPort's shape (BankLinks -> Customers), just in the opposite
    /// direction (Customers -> legacy Transactions).
    /// </summary>
    public interface IAccountBalanceProvider
    {
        Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<Guid, decimal>> GetBalancesByCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default);
    }
}
