using BuildingBlocks.Application.Abstractions.Authentication;

namespace Modules.Transactions.Application.Common.Aggregation
{
    // Every read of the ledger: whose data the caller may see (Access), optionally narrowed to one
    // bank (its code, e.g. "FNB") and one of that bank's accounts. An account id is only unique
    // within its bank, so it is never used without the institution.
    public sealed record TransactionFilter(InstitutionAccess Access, string? Institution = null, string? ExternalAccountId = null);
}