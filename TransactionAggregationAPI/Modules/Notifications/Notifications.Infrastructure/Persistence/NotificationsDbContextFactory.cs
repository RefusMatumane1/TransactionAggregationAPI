using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Modules.Audit.Contracts;

namespace Modules.Notifications.Infrastructure.Persistence
{
    public class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
    {
        public NotificationsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
            optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

            return new NotificationsDbContext(optionsBuilder.Options, new UnavailableAuditTrail());
        }
    }
}