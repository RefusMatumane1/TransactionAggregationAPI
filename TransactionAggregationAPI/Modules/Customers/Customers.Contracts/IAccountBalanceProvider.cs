namespace Modules.Customers.Contracts
{
    /// <summary>
    /// Owned by Customers, implemented by Transactions (TransactionBalanceProvider): an account's
    /// balance is derived from its transactions, which Customers can't read directly.
    /// </summary>
    public interface IAccountBalanceProvider
    {
        Task<AccountBalance> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<Guid, AccountBalance>> GetBalancesByCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Booked = what the bank has posted (the balance proper). Pending authorisations are
    /// kept apart, split by direction, because they affect what's spendable differently:
    /// money on its way out is already unavailable, money on its way in isn't available yet.
    /// </summary>
    /// <param name="PendingDebits">Sum of pending outflows (zero or negative).</param>
    /// <param name="PendingCredits">Sum of pending inflows (zero or positive).</param>
    public sealed record AccountBalance(decimal Booked, decimal PendingDebits, decimal PendingCredits)
    {
        public static AccountBalance Zero { get; } = new(0m, 0m, 0m);

        /// <summary>Net of everything pending, both directions.</summary>
        public decimal Pending => PendingDebits + PendingCredits;

        /// <summary>Booked balance less pending outflows — the usual "available balance".</summary>
        public decimal Available => Booked + PendingDebits;
    }
}