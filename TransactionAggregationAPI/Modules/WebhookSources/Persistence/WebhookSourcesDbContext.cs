using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;

namespace Modules.WebhookSources.Persistence
{
    /// <summary>
    /// Owns its own "webhooksources" Postgres schema and its own EF Core migrations
    /// history, independent of every other module's DbContext — see
    /// docs/adr/0009-schema-per-module-database-strategy.md. No cross-context
    /// transaction sharing needed here (unlike ApplicationDbContext/MessagingDbContext):
    /// every write in this module (create/activate/deactivate/rotate a webhook source,
    /// or record its usage in ApiKeyEndpointFilter) is a standalone unit of work with
    /// no other module's data changing alongside it.
    /// </summary>
    public class WebhookSourcesDbContext : AppDbContextBase, IWebhookSourcesDbContext
    {
        public WebhookSourcesDbContext(DbContextOptions<WebhookSourcesDbContext> options, IMediator mediator)
            : base(options, mediator)
        {
        }

        public DbSet<WebhookSource> WebhookSources => Set<WebhookSource>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("webhooksources");

            modelBuilder.ApplyConfiguration(new WebhookSourceConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}
