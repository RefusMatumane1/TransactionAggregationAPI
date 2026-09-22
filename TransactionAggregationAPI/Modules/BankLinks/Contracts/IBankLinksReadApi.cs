namespace Modules.BankLinks.Contracts
{
    /// <summary>
    /// Published read contract for other modules — deliberately narrower than exposing
    /// IBankLinksDbContext's DbSet (which stays internal to this module). Returns
    /// primitives/DTOs, never the BankLink entity, so consumers can't reach through
    /// into this module's internals the way the old shared IApplicationDbContext let
    /// handlers reach into other modules' DbSets.
    /// </summary>
    public interface IBankLinksReadApi
    {
        Task<ActiveBankLinkInfo?> FindActiveLinkByExternalAccountIdAsync(
            string externalAccountId,
            CancellationToken cancellationToken = default);
    }

    public sealed record ActiveBankLinkInfo(
        Guid BankLinkId,
        Guid CustomerId,
        Guid AccountId,
        string InstitutionName);
}
