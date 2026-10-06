using BuildingBlocks.Application.Abstractions.Authentication;

namespace Modules.Transactions.Application.Common.Aggregation
{
    // An account id is unique only within its bank, so it is never used without the institution.
    // Accounts: a customer's linked (bank, account) pairs; null is unrestricted, empty matches nothing.
    public sealed record TransactionFilter(
        InstitutionAccess Access,
        string? Institution = null,
        string? ExternalAccountId = null,
        IReadOnlyList<AccountKey>? Accounts = null)
    {
        public const int MaxAccounts = 50;
    }

    public sealed record AccountKey(string Institution, string ExternalAccountId);
}