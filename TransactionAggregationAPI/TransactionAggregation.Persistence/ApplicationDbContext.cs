using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Inbox;
using TransactionAggregation.Domain.Outbox;
using TransactionAggregation.Persistence.Configurations;

namespace TransactionAggregation.Persistence
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        private readonly IMediator _mediator;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IMediator mediator)
            : base(options)
        {
            _mediator = mediator;
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<BankLink> BankLinks => Set<BankLink>();
        public DbSet<WebhookSource> WebhookSources => Set<WebhookSource>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
            modelBuilder.ApplyConfiguration(new CustomerConfiguration());
            modelBuilder.ApplyConfiguration(new AccountConfiguration());
            modelBuilder.ApplyConfiguration(new BankLinkConfiguration());
            modelBuilder.ApplyConfiguration(new WebhookSourceConfiguration());
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
            modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

            base.OnModelCreating(modelBuilder);
            modelBuilder.Ignore<BaseDomainEvent>();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Update audit fields
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

        public Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)OutboxMessageStatus.Pending;
            var processing = (int)OutboxMessageStatus.Processing;

            // One atomic UPDATE ... RETURNING: the row selection and the Status flip happen in a
            // single statement, so SKIP LOCKED lets concurrent callers each grab a disjoint batch
            // instead of blocking on each other or double-claiming the same rows.
            return OutboxMessages.FromSqlInterpolated($@"
                UPDATE ""OutboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""OutboxMessages""
                    WHERE ""Status"" = {pending}
                      AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now})
                    ORDER BY ""OccurredAt""
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *;")
                .ToListAsync(cancellationToken);
        }

        public Task<List<InboxMessage>> ClaimInboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)InboxMessageStatus.Pending;
            var processing = (int)InboxMessageStatus.Processing;

            // Same atomic claim shape as ClaimOutboxMessagesAsync — one UPDATE ... RETURNING with
            // SKIP LOCKED so concurrent callers each claim a disjoint batch.
            return InboxMessages.FromSqlInterpolated($@"
                UPDATE ""InboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""InboxMessages""
                    WHERE ""Status"" = {pending}
                      AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now})
                    ORDER BY ""ReceivedAt""
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *;")
                .ToListAsync(cancellationToken);
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
