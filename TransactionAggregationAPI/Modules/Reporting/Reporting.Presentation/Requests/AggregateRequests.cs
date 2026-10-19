using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using Modules.Currency.Contracts;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;
using Modules.Reporting.Application.Features.Aggregates;

namespace Modules.Reporting.Presentation.Requests
{
    public sealed record CategoryBreakdownRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        CashFlowDirection Direction = CashFlowDirection.Expense,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetCategoryBreakdownQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new AccountFilter(access, Institution, ExternalAccountId), Direction, Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }

    public sealed record CashFlowRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        TimeGranularity Granularity = TimeGranularity.Month,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetCashFlowQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new AccountFilter(access, Institution, ExternalAccountId), Granularity, Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }

    public sealed record InstitutionBreakdownRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetInstitutionBreakdownQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new AccountFilter(access, Institution), Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }

    public sealed record PeriodComparisonRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetPeriodComparisonQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new AccountFilter(access, Institution, ExternalAccountId), Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }
}