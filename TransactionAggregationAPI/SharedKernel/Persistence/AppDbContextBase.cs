using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Common;

namespace SharedKernel.Persistence
{
    /// <summary>
    /// Every per-module DbContext inherits this instead of duplicating the same
    /// CreatedAt/UpdatedAt stamping + domain-event dispatch that
    /// TransactionAggregation.Persistence/ApplicationDbContext.cs already implemented
    /// generically over ChangeTracker.Entries&lt;BaseEntity&gt;() — the logic itself
    /// never referenced any specific entity type, so it belongs here, not repeated
    /// per module.
    /// </summary>
    public abstract class AppDbContextBase : DbContext
    {
        private readonly IMediator _mediator;

        protected AppDbContextBase(DbContextOptions options, IMediator mediator)
            : base(options)
        {
            _mediator = mediator;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<BaseDomainEvent>();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        break;
                }
            }

            await DispatchDomainEvents();

            return await base.SaveChangesAsync(cancellationToken);
        }

        private async Task DispatchDomainEvents()
        {
            var domainEntities = ChangeTracker
                .Entries<BaseEntity>()
                .Where(x => x.Entity.DomainEvents.Any())
                .Select(x => x.Entity)
                .ToList();

            var domainEvents = domainEntities
                .SelectMany(x => x.DomainEvents)
                .ToList();

            domainEntities.ForEach(entity => entity.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent, cancellationToken: default);
            }
        }
    }
}
