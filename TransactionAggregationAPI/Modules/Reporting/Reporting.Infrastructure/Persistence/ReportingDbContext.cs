using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.Persistence;

namespace Modules.Reporting.Infrastructure.Persistence
{
    public class ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : AppDbContextBase(options), IReportingDbContext
    {
        public const string Schema = "reporting";

        public DbSet<DailyTotal> DailyTotals => Set<DailyTotal>();

        public DbSet<AggregationCheckpoint> AggregationCheckpoints => Set<AggregationCheckpoint>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.ApplyConfiguration(new DailyTotalConfiguration());
            modelBuilder.ApplyConfiguration(new AggregationCheckpointConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
