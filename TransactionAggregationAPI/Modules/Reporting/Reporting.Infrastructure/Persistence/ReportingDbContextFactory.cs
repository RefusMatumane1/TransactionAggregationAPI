using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Reporting.Infrastructure.Persistence
{
    public class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
    {
        public ReportingDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
            optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

            return new ReportingDbContext(optionsBuilder.Options);
        }
    }
}
