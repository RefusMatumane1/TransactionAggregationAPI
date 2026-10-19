using Microsoft.EntityFrameworkCore;
using Modules.Reporting.Application.Aggregation;

namespace Modules.Reporting.Application.Persistence
{
    public interface IReportingDbContext
    {
        DbSet<DailyTotal> DailyTotals { get; }

        DbSet<AggregationCheckpoint> AggregationCheckpoints { get; }
    }
}
