using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Aggregation
{
    // The read model every aggregate query reads: one row per South African calendar day, account,
    // category and currency, totalling that day's ledger entries. Written only by the scheduled
    // refresh (IDailyTotalsRefresher), never on the request path.
    public sealed class DailyTotal
    {
        public DateOnly Day { get; init; }
        public string SourceName { get; init; } = null!;
        public string ExternalAccountId { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public string Currency { get; init; } = null!;

        // Both positive: credits and debits are kept apart so a breakdown never nets them.
        public decimal Income { get; init; }
        public decimal Expenses { get; init; }
        public int IncomeCount { get; init; }
        public int ExpenseCount { get; init; }
    }

    // A single row recording how far the read model has been built.
    public sealed class AggregationCheckpoint
    {
        public const int SingletonId = 1;

        public int Id { get; init; }

        // Ledger rows recorded (CreatedAt) after this have not been folded in yet.
        public DateTime Watermark { get; init; }

        // When the last refresh started: the totals reflect the ledger as recorded up to this time.
        public DateTime AsOf { get; init; }
    }
}