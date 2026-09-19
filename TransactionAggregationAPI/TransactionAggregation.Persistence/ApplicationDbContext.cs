using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel.Common;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Persistence.Configurations;

namespace TransactionAggregation.Persistence
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        private readonly IMediator _mediator;
        private readonly MessagingDbContext _messagingDbContext;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IMediator mediator,
            MessagingDbContext messagingDbContext)
            : base(options)
        {
            _mediator = mediator;
            _messagingDbContext = messagingDbContext;
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<BankLink> BankLinks => Set<BankLink>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
            modelBuilder.ApplyConfiguration(new CustomerConfiguration());
            modelBuilder.ApplyConfiguration(new AccountConfiguration());
            modelBuilder.ApplyConfiguration(new BankLinkConfiguration());

            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<BaseDomainEvent>();
        }

        /// <summary>
        /// Domain-event handlers dispatched below (e.g. TransactionCreatedEventHandler)
        /// write an OutboxMessage to the separate MessagingDbContext/"messaging" schema
        /// — see docs/adr/0009-schema-per-module-database-strategy.md. ADR-0003 requires
        /// that write commit atomically with this context's own changes (the whole point
        /// of the Outbox pattern), so both contexts are registered against one shared
        /// NpgsqlConnection (Program.cs) and explicitly share one transaction here.
        /// EnableRetryOnFailure forbids a manually-managed transaction unless it runs
        /// inside CreateExecutionStrategy().ExecuteAsync — hence the wrapper below.
        /// NOT verified against a live Postgres (no Docker in this session) — test this
        /// path specifically before trusting it: a crash between the two contexts'
        /// SaveChangesAsync calls must leave neither committed, not just the DB one.
        ///
        /// The in-memory provider (unit/integration tests) doesn't support real
        /// transactions at all — BeginTransactionAsync throws there — so the
        /// transaction-sharing dance only runs against a real relational database;
        /// tests fall back to calling both contexts' SaveChangesAsync in sequence,
        /// which is enough to exercise the actual business logic even though it can't
        /// exercise the atomicity guarantee itself (only a real Postgres run can).
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

            if (!Database.IsRelational())
            {
                await DispatchDomainEvents();
                var nonRelationalResult = await base.SaveChangesAsync(cancellationToken);
                await _messagingDbContext.SaveChangesAsync(cancellationToken);
                return nonRelationalResult;
            }

            var strategy = Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                await _messagingDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

                await DispatchDomainEvents();

                var result = await base.SaveChangesAsync(cancellationToken);

                // Sharing the transaction above does not share change-tracking: a
                // caller (or a domain-event handler dispatched above) may have added
                // entities to _messagingDbContext that still need flushing before this
                // transaction commits — otherwise they're silently never persisted.
                await _messagingDbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return result;
            });
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
