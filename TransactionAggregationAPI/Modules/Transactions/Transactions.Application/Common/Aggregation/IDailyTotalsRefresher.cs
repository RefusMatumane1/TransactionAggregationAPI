namespace Modules.Transactions.Application.Common.Aggregation
{
    public interface IDailyTotalsRefresher
    {
        // Recomputes every account-day with entries since the last refresh (reaching back `overlap` for late commits).
        // Idempotent; null when another replica is refreshing.
        Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, CancellationToken cancellationToken);
    }

    public sealed record DailyTotalsRefresh(int AccountDaysRecomputed, DateTime AsOf);
}