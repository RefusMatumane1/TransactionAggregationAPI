using BuildingBlocks.Application.Abstractions.Authentication;

namespace Modules.Transactions.Application.Common.Aggregation
{
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