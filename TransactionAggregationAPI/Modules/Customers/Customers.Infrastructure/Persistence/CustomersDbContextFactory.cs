using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Modules.Audit.Contracts;

namespace Modules.Customers.Infrastructure.Persistence
{
    public class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
    {
        public CustomersDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<CustomersDbContext>();
            optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

            return new CustomersDbContext(optionsBuilder.Options, new UnavailableAuditTrail());
        }
    }
}