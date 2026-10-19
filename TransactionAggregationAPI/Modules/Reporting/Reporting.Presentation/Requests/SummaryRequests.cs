using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.Features.GetTransactionSummary;

namespace Modules.Reporting.Presentation.Requests
{
    public sealed record TransactionSummaryRequest(
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetTransactionSummaryQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var now = time.GetUtcNow().UtcDateTime;
            return new(StartDate ?? now.AddMonths(-12), EndDate ?? now,
                new AccountFilter(access, Institution, ExternalAccountId), Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }
}
