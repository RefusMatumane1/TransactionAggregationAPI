using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Common;

namespace SharedKernel.Persistence
{
    /// <summary>CreatedAt/UpdatedAt stamping and domain-event dispatch shared by every module DbContext.</summary>
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

        /// <summary>
        /// Overrides the (bool, CancellationToken) overload — the one every other SaveChanges
        /// overload funnels into — so stamping and event dispatch also run when a caller defers
        /// AcceptAllChanges until its own transaction commits (see TransactionsDbContext).
        /// </summary>
        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
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

            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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