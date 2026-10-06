using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Queries.Aggregates;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Presentation.Requests
{
    public sealed record CategoryBreakdownRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        CashFlowDirection Direction = CashFlowDirection.Expense,
        string Currency = SupportedCurrency.Default)
    {
        internal GetCategoryBreakdownQuery ToQuery(InstitutionAccess access, TimeProvider time)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new TransactionFilter(access, Institution, ExternalAccountId), Direction, Currency);
        }
    }

    public sealed record CashFlowRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        TimeGranularity Granularity = TimeGranularity.Month,
        string Currency = SupportedCurrency.Default)
    {
        internal GetCashFlowQuery ToQuery(InstitutionAccess access, TimeProvider time)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new TransactionFilter(access, Institution, ExternalAccountId), Granularity, Currency);
        }
    }

    public sealed record InstitutionBreakdownRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string Currency = SupportedCurrency.Default)
    {
        internal GetInstitutionBreakdownQuery ToQuery(InstitutionAccess access, TimeProvider time)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new TransactionFilter(access, Institution), Currency);
        }
    }

    public sealed record PeriodComparisonRequest(
        DateOnly? From = null,
        DateOnly? To = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string Currency = SupportedCurrency.Default)
    {
        internal GetPeriodComparisonQuery ToQuery(InstitutionAccess access, TimeProvider time)
        {
            var period = ReportingPeriod.Resolve(From, To, time);
            return new(period.From, period.To, new TransactionFilter(access, Institution, ExternalAccountId), Currency);
        }
    }
}