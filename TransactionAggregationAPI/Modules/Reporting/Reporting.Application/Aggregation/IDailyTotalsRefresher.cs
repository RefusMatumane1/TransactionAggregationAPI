
namespace Modules.Reporting.Application.Aggregation
{
    public interface IDailyTotalsRefresher
    {
        Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, int maxRows, CancellationToken cancellationToken);
    }

    public sealed record DailyTotalsRefresh(int AccountDaysRecomputed, DateTime AsOf, bool HasMore = false);
}