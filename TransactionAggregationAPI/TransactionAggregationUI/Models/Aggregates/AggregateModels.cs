using TransactionAggregationUI.Models.Transactions;

namespace TransactionAggregationUI.Models.Aggregates
{
    public class ReportFilter
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public string? Institution { get; set; }
        public string? ExternalAccountId { get; set; }

        public Guid? CustomerId { get; set; }
    }

    public static class ReportRange
    {
        public const string ThisMonth = "month";
        public const string LastThreeMonths = "3m";
        public const string YearToDate = "ytd";
        public const string LastTwelveMonths = "12m";
        public const string Custom = "custom";
        public const string Default = LastTwelveMonths;

        public static readonly IReadOnlyList<(string Key, string Label)> Presets =
        [
            (ThisMonth, "This month"),
            (LastThreeMonths, "3 months"),
            (YearToDate, "Year to date"),
            (LastTwelveMonths, "12 months")
        ];

        public static (DateOnly From, DateOnly To)? Resolve(string? key, DateOnly today) => key switch
        {
            ThisMonth => (new DateOnly(today.Year, today.Month, 1), today),
            LastThreeMonths => (new DateOnly(today.Year, today.Month, 1).AddMonths(-2), today),
            YearToDate => (new DateOnly(today.Year, 1, 1), today),
            LastTwelveMonths => (new DateOnly(today.Year, today.Month, 1).AddMonths(-11), today),
            _ => null
        };
    }

    public class PeriodModel
    {
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
    }

    public class InstitutionBreakdownModel
    {
        public PeriodModel Period { get; set; } = new();
        public string Currency { get; set; } = "ZAR";
        public DateTime? AsOf { get; set; }
        public List<InstitutionTotalModel> Institutions { get; set; } = [];
    }

    public class InstitutionTotalModel
    {
        public string Institution { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
        public int TransactionCount { get; set; }
        public int AccountCount { get; set; }
        public List<AccountTotalModel> Accounts { get; set; } = [];
    }

    public class AccountTotalModel
    {
        public string ExternalAccountId { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
        public int TransactionCount { get; set; }
    }

    public class CategoryBreakdownModel
    {
        public PeriodModel Period { get; set; } = new();
        public string Currency { get; set; } = "ZAR";
        public DateTime? AsOf { get; set; }
        public decimal Total { get; set; }
        public int TransactionCount { get; set; }
        public List<CategoryTotalModel> Categories { get; set; } = [];
    }

    public class CategoryTotalModel
    {
        public TransactionCategory Category { get; set; }
        public decimal Amount { get; set; }
        public int TransactionCount { get; set; }
        public decimal Share { get; set; }
    }

    public class CashFlowSeriesModel
    {
        public PeriodModel Period { get; set; } = new();
        public string Currency { get; set; } = "ZAR";
        public DateTime? AsOf { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal Net { get; set; }
        public List<CashFlowPointModel> Points { get; set; } = [];
    }

    public class CashFlowPointModel
    {
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
        public int TransactionCount { get; set; }
    }

    public class PeriodComparisonModel
    {
        public string Currency { get; set; } = "ZAR";
        public DateTime? AsOf { get; set; }
        public PeriodTotalsModel Current { get; set; } = new();
        public PeriodTotalsModel Previous { get; set; } = new();
        public decimal? ExpensesChangePercent { get; set; }
        public decimal? IncomeChangePercent { get; set; }
        public List<CategoryChangeModel> Categories { get; set; } = [];
    }

    public class PeriodTotalsModel
    {
        public PeriodModel Period { get; set; } = new();
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
        public int TransactionCount { get; set; }
    }

    public class CategoryChangeModel
    {
        public TransactionCategory Category { get; set; }
        public decimal CurrentSpend { get; set; }
        public decimal PreviousSpend { get; set; }
        public decimal Change { get; set; }
        public decimal? ChangePercent { get; set; }
    }
}