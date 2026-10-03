namespace Modules.Transactions.Application.Common.Aggregation
{
    public interface IDailyTotalsRefresher
    {
        // Recomputes, from the ledger, every account-day that received entries since the last
        // refresh (reaching back `overlap` for rows committed late), and moves the checkpoint.
        // Idempotent. Returns null when another replica is already refreshing.
        Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, CancellationToken cancellationToken);
    }

    public sealed record DailyTotalsRefresh(int AccountDaysRecomputed, DateTime AsOf);
}