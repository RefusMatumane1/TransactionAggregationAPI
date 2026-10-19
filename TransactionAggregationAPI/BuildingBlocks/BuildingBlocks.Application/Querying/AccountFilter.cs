using BuildingBlocks.Application.Abstractions.Authentication;

namespace BuildingBlocks.Application.Querying
{
    /// <summary>
    /// Which bank accounts a query may see: what the caller is allowed to read (<see cref="Access"/>), narrowed by an
    /// optional bank, account, or explicit set of accounts. Account ids are only unique within their bank, so an
    /// account is always named together with its institution.
    /// </summary>
    public sealed record AccountFilter(
        InstitutionAccess Access,
        string? Institution = null,
        string? ExternalAccountId = null,
        IReadOnlyList<AccountKey>? Accounts = null)
    {
        public const int MaxAccounts = 50;

        public const int MaxInstitutionLength = 50;

        public const int MaxExternalAccountIdLength = 200;
    }

    public sealed record AccountKey(string Institution, string ExternalAccountId);
}
