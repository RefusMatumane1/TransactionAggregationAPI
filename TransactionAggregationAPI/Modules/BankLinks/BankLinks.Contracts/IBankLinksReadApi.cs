namespace Modules.BankLinks.Contracts
{
    /// <summary>Read contract for other modules: returns DTOs, never the BankLink entity or its DbSet.</summary>
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