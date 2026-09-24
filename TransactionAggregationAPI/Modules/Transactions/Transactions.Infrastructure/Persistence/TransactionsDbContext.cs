using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Infrastructure.Persistence.Configurations;
using SharedKernel.Common;

namespace Modules.Transactions.Infrastructure.Persistence
{
    public class TransactionsDbContext : DbContext, ITransactionsDbContext
    {
        private readonly IMediator _mediator;
        private readonly MessagingDbContext _messagingDbContext;

        public TransactionsDbContext(
            DbContextOptions<TransactionsDbContext> options,
            IMediator mediator,
            MessagingDbContext messagingDbContext)
            : base(options)
        {
            _mediator = mediator;
            _messagingDbContext = messagingDbContext;
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("transactions");

            modelBuilder.ApplyConfiguration(new TransactionConfiguration());

            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<BaseDomainEvent>();
        }

        /// <summary>
        /// Commits this context's changes and the outbox/inbox rows staged on the shared
        /// MessagingDbContext in one transaction (ADR-0003, ADR-0009).
        ///
        /// The retrying execution strategy re-runs the whole lambda after a transient failure, so both
        /// saves defer AcceptAllChanges until the commit has succeeded. Otherwise a failed commit would
        /// leave the rows marked Unchanged and the retry would commit the outbox rows without them
        /// (IngestionIntegrityTests). The in-memory provider has no transactions, so tests save the
        /// two contexts in sequence.
        /// </summary>
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

            // Once, outside the retried lambda: the events' handlers only stage outbox rows,
            // and those stay staged (not accepted) across retries until the commit succeeds.
            await DispatchDomainEvents();

            if (!Database.IsRelational())
            {
                var nonRelationalResult = await base.SaveChangesAsync(cancellationToken);
                await _messagingDbContext.SaveChangesAsync(cancellationToken);
                return nonRelationalResult;
            }

            var strategy = Database.CreateExecutionStrategy();

            var result = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                await _messagingDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

                try
                {
                    var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);

                    // Sharing the transaction does not share change-tracking: rows staged on
                    // _messagingDbContext (by the caller or a domain-event handler) must be
                    // flushed before this transaction commits, or they're never persisted.
                    await _messagingDbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);

                    await transaction.CommitAsync(cancellationToken);

                    return saved;
                }
                finally
                {
                    // Otherwise _messagingDbContext keeps pointing at this (now committed or
                    // rolled-back) transaction, and its next standalone SaveChangesAsync in the
                    // same scope would try to enlist in it.
                    await _messagingDbContext.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            });

            ChangeTracker.AcceptAllChanges();
            _messagingDbContext.ChangeTracker.AcceptAllChanges();

            return result;
        }

        public void DiscardPendingChanges()
        {
            ChangeTracker.Clear();

            // Only Added rows — the inbox dispatcher shares this MessagingDbContext and still
            // needs its tracked InboxMessages (claimed, then marked processed/failed).
            foreach (var entry in _messagingDbContext.ChangeTracker.Entries<OutboxMessage>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
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
                await _mediator.Publish(domainEvent);
            }
        }
    }
}