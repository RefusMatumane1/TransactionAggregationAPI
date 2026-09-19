using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Persistence;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;

namespace BuildingBlocks.Messaging.Persistence
{
    /// <summary>
    /// Owns its own "messaging" Postgres schema and its own EF Core migrations
    /// history, independent of every business module's DbContext — see
    /// docs/adr/0009-schema-per-module-database-strategy.md.
    /// </summary>
    public class MessagingDbContext : AppDbContextBase, IMessagingDbContext
    {
        public MessagingDbContext(DbContextOptions<MessagingDbContext> options, IMediator mediator)
            : base(options, mediator)
        {
        }

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("messaging");

            modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

            base.OnModelCreating(modelBuilder);
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
                UPDATE ""messaging"".""OutboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""messaging"".""OutboxMessages""
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
                UPDATE ""messaging"".""InboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""messaging"".""InboxMessages""
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
    }
}
