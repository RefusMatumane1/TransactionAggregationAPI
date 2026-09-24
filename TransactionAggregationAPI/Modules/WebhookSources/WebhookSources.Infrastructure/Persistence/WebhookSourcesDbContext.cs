using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
using SharedKernel.Persistence;

namespace Modules.WebhookSources.Infrastructure.Persistence
{
    /// <summary>Owns the "webhooksources" schema and its own migrations history (ADR-0009).</summary>
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