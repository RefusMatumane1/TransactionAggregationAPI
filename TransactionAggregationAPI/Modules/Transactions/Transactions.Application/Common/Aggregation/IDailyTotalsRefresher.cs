namespace Modules.Transactions.Application.Common.Aggregation
{
    public interface IDailyTotalsRefresher
    {
        // Recomputes every account-day with entries since the last refresh (reaching back `overlap` for late commits),
        // folding in at most `maxRows` new entries per call; HasMore says a backlog remains. Keep `overlap` above zero
        // when capped, so entries sharing the cut-off timestamp are picked up by the next call.
        // Idempotent; null when another replica is refreshing.
        Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, int maxRows, CancellationToken cancellationToken);
    }

    public sealed record DailyTotalsRefresh(int AccountDaysRecomputed, DateTime AsOf, bool HasMore = false);
}