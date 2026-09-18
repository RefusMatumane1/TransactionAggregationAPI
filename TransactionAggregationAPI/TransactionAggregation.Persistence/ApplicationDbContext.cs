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

        public Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)OutboxMessageStatus.Pending;
            var processing = (int)OutboxMessageStatus.Processing;
            var staleClaimCutoff = now.Subtract(claimTimeout);

            // Also reclaims messages stuck in Processing past claimTimeout — the
            // dispatcher's own claim UPDATE commits immediately (its own statement),
            // separately from the SaveChangesAsync() that later marks a message
            // Processed/Failed. If the dispatcher crashes in between, a message would
            // otherwise stay Processing forever with no automatic recovery.
            return OutboxMessages.FromSqlInterpolated($@"
                UPDATE ""OutboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""OutboxMessages""
                    WHERE (
                        ""Status"" = {pending}
                        AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now})
                    )
                    OR (
                        ""Status"" = {processing}
                        AND ""ClaimedAt"" <= {staleClaimCutoff}
                    )
                    ORDER BY ""OccurredAt""
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *;")
                            .ToListAsync(cancellationToken);
        }

        public Task<List<InboxMessage>> ClaimInboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)InboxMessageStatus.Pending;
            var processing = (int)InboxMessageStatus.Processing;
            var staleClaimCutoff = now.Subtract(claimTimeout);

            // See the comment in ClaimOutboxMessagesAsync — same stale-claim reclaim reasoning.
            return InboxMessages.FromSqlInterpolated($@"
                UPDATE ""InboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""InboxMessages""
                    WHERE (
                        ""Status"" = {pending}
                        AND (""NextAttemptAt"" IS NULL OR ""NextAttemptAt"" <= {now})
                    )
                    OR (
                        ""Status"" = {processing}
                        AND ""ClaimedAt"" <= {staleClaimCutoff}
                    )
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