using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Aggregation
{
    public sealed class DailyTotal
    {
        public DateOnly Day { get; init; }
        public string SourceName { get; init; } = null!;
        public string ExternalAccountId { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public string Currency { get; init; } = null!;

        public decimal Income { get; init; }
        public decimal Expenses { get; init; }
        public int IncomeCount { get; init; }
        public int ExpenseCount { get; init; }
    }

    public sealed class AggregationCheckpoint
    {
        public const int SingletonId = 1;

        public int Id { get; init; }

        public DateTime Watermark { get; init; }

        public DateTime AsOf { get; init; }
    }
}