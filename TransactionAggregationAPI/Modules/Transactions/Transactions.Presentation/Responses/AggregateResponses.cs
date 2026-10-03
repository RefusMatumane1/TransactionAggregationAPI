using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Presentation.Responses
{
    // AsOf on every aggregate: the totals come from the scheduled daily read model and include the
    // ledger as recorded up to that time (null until the first refresh has run).
    public sealed record PeriodResponse(DateOnly From, DateOnly To)
    {
        internal static PeriodResponse Of(PeriodDto period) => new(period.From, period.To);
    }

    public sealed record CategoryTotalResponse(TransactionCategory Category, string CategoryName, decimal Amount, int TransactionCount, decimal Share);

    public sealed record CategoryBreakdownResponse(
        PeriodResponse Period,
        string Currency,
        CashFlowDirection Direction,
        decimal Total,
        int TransactionCount,
        IReadOnlyList<CategoryTotalResponse> Categories,
        DateTime? AsOf)
    {
        internal static CategoryBreakdownResponse From(CategoryBreakdownDto dto) => new(
            PeriodResponse.Of(dto.Period),
            dto.Currency,
            dto.Direction,
            dto.Total,
            dto.TransactionCount,
            dto.Categories.Select(c => new CategoryTotalResponse(c.Category, c.Category.ToString(), c.Amount, c.TransactionCount, c.Share)).ToList(),
            dto.AsOf);
    }

    public sealed record CashFlowPointResponse(DateOnly PeriodStart, DateOnly PeriodEnd, decimal Income, decimal Expenses, decimal Net, int TransactionCount);

    public sealed record CashFlowSeriesResponse(
        PeriodResponse Period,
        string Currency,
        TimeGranularity Granularity,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal Net,
        IReadOnlyList<CashFlowPointResponse> Points,
        DateTime? AsOf)
    {
        internal static CashFlowSeriesResponse From(CashFlowSeriesDto dto) => new(
            PeriodResponse.Of(dto.Period),
            dto.Currency,
            dto.Granularity,
            dto.TotalIncome,
            dto.TotalExpenses,
            dto.Net,
            dto.Points.Select(p => new CashFlowPointResponse(p.PeriodStart, p.PeriodEnd, p.Income, p.Expenses, p.Net, p.TransactionCount)).ToList(),
            dto.AsOf);
    }

    public sealed record AccountTotalResponse(string ExternalAccountId, decimal Income, decimal Expenses, decimal Net, int TransactionCount);

    public sealed record InstitutionTotalResponse(
        string Institution,
        decimal Income,
        decimal Expenses,
        decimal Net,
        int TransactionCount,
        IReadOnlyList<AccountTotalResponse> Accounts);

    public sealed record InstitutionBreakdownResponse(
        PeriodResponse Period, string Currency, IReadOnlyList<InstitutionTotalResponse> Institutions, DateTime? AsOf)
    {
        internal static InstitutionBreakdownResponse From(InstitutionBreakdownDto dto) => new(
            PeriodResponse.Of(dto.Period),
            dto.Currency,
            dto.Institutions.Select(i => new InstitutionTotalResponse(
                i.Institution, i.Income, i.Expenses, i.Net, i.TransactionCount,
                i.Accounts.Select(a => new AccountTotalResponse(a.ExternalAccountId, a.Income, a.Expenses, a.Net, a.TransactionCount)).ToList())).ToList(),
            dto.AsOf);
    }

    public sealed record PeriodTotalsResponse(PeriodResponse Period, decimal Income, decimal Expenses, decimal Net, int TransactionCount)
    {
        internal static PeriodTotalsResponse From(PeriodTotalsDto dto) =>
            new(PeriodResponse.Of(dto.Period), dto.Income, dto.Expenses, dto.Net, dto.TransactionCount);
    }

    public sealed record CategoryChangeResponse(
        TransactionCategory Category,
        string CategoryName,
        decimal CurrentSpend,
        decimal PreviousSpend,
        decimal Change,
        decimal? ChangePercent);

    public sealed record PeriodComparisonResponse(
        string Currency,
        PeriodTotalsResponse Current,
        PeriodTotalsResponse Previous,
        decimal? ExpensesChangePercent,
        decimal? IncomeChangePercent,
        IReadOnlyList<CategoryChangeResponse> Categories,
        DateTime? AsOf)
    {
        internal static PeriodComparisonResponse From(PeriodComparisonDto dto) => new(
            dto.Currency,
            PeriodTotalsResponse.From(dto.Current),
            PeriodTotalsResponse.From(dto.Previous),
            dto.ExpensesChangePercent,
            dto.IncomeChangePercent,
            dto.Categories.Select(c => new CategoryChangeResponse(
                c.Category, c.Category.ToString(), c.CurrentSpend, c.PreviousSpend, c.Change, c.ChangePercent)).ToList(),
            dto.AsOf);
    }
}