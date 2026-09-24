namespace Modules.BankLinks.Contracts
{
    /// <summary>
    /// Published read contract for other modules — deliberately narrower than exposing
    /// IBankLinksDbContext's DbSet (which stays internal to this module). Returns
    /// primitives/DTOs, never the BankLink entity, so consumers can't reach through
    /// into this module's internals the way the old shared ITransactionsDbContext let
    /// handlers reach into other modules' DbSets.
    /// </summary>
    public interface IBankLinksReadApi
    {
        /// <summary>
        /// Every active link to this bank account, oldest first. More than one is normal: each
        /// holder of a joint account links it separately, and each is entitled to its
        /// transactions. Empty when nobody has linked the account.
        /// </summary>
        Task<IReadOnlyList<ActiveBankLinkInfo>> FindActiveLinksByExternalAccountIdAsync(
            string externalAccountId,
            CancellationToken cancellationToken = default);
    }

    public sealed record ActiveBankLinkInfo(
        Guid BankLinkId,
        Guid CustomerId,
        Guid AccountId,
        string InstitutionName);
}