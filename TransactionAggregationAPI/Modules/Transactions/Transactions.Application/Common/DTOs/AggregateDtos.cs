using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    public enum CashFlowDirection
    {
        Expense,
        Income
    }

    public enum TimeGranularity
    {
        Day,
        Week,
        Month
    }

    public sealed record PeriodDto(DateOnly From, DateOnly To);

    public sealed record CategoryTotalDto(TransactionCategory Category, decimal Amount, int TransactionCount, decimal Share);

    public sealed record CategoryBreakdownDto(
        PeriodDto Period,
        string Currency,
        CashFlowDirection Direction,
        decimal Total,
        int TransactionCount,
        IReadOnlyList<CategoryTotalDto> Categories,
        DateTime? AsOf);

    public sealed record CashFlowPointDto(
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        decimal Income,
        decimal Expenses,
        decimal Net,
        int TransactionCount);

    public sealed record CashFlowSeriesDto(
        PeriodDto Period,
        string Currency,
        TimeGranularity Granularity,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal Net,
        IReadOnlyList<CashFlowPointDto> Points,
        DateTime? AsOf);

    public sealed record AccountTotalDto(string ExternalAccountId, decimal Income, decimal Expenses, decimal Net, int TransactionCount);

    public sealed record InstitutionTotalDto(
        string Institution,
        decimal Income,
        decimal Expenses,
        decimal Net,
        int TransactionCount,
        int AccountCount,
        IReadOnlyList<AccountTotalDto> Accounts);

    public sealed record InstitutionBreakdownDto(
        PeriodDto Period, string Currency, IReadOnlyList<InstitutionTotalDto> Institutions, DateTime? AsOf);

    public sealed record PeriodTotalsDto(PeriodDto Period, decimal Income, decimal Expenses, decimal Net, int TransactionCount);

    public sealed record CategoryChangeDto(
        TransactionCategory Category,
        decimal CurrentSpend,
        decimal PreviousSpend,
        decimal Change,
        decimal? ChangePercent);

    public sealed record PeriodComparisonDto(
        string Currency,
        PeriodTotalsDto Current,
        PeriodTotalsDto Previous,
        decimal? ExpensesChangePercent,
        decimal? IncomeChangePercent,
        IReadOnlyList<CategoryChangeDto> Categories,
        DateTime? AsOf);
}