using BuildingBlocks.Messaging.Archiving;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Messaging.Persistence
{
    public class MessagingDbContext : AppDbContextBase, IMessagingDbContext, IMessageArchive
    {
        // A claim that expired with no attempts left: the worker died while processing the message
        // on every attempt, so the message itself is the likely cause.
        public const string AbandonedClaimError =
            "Claim expired with no attempts left: processing never completed, so the message is likely what stops the worker";

        public MessagingDbContext(DbContextOptions<MessagingDbContext> options)
            : base(options)
        {
        }

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<ArchivedInboxMessage> ArchivedInboxMessages => Set<ArchivedInboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("messaging");

            modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
            modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
            modelBuilder.ApplyConfiguration(new ArchivedInboxMessageConfiguration());
            modelBuilder.ApplyConfiguration(new ArchivedOutboxMessageConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public async Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, int maxAttempts, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)OutboxMessageStatus.Pending;
            var processing = (int)OutboxMessageStatus.Processing;
            var deadLettered = (int)OutboxMessageStatus.DeadLettered;
            var staleClaimCutoff = now.Subtract(claimTimeout);
            var abandoned = AbandonedClaimError;

            await Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""messaging"".""OutboxMessages""
                SET ""Status"" = {deadLettered}, ""ClaimedAt"" = NULL, ""LastError"" = {abandoned}
                WHERE ""Status"" = {processing} AND ""ClaimedAt"" <= {staleClaimCutoff} AND ""Attempts"" >= {maxAttempts};",
                cancellationToken);

            return await OutboxMessages.FromSqlInterpolated($@"
                UPDATE ""messaging"".""OutboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}, ""Attempts"" = ""Attempts"" + 1
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

        public async Task<List<InboxMessage>> ClaimInboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, int maxAttempts, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var pending = (int)InboxMessageStatus.Pending;
            var processing = (int)InboxMessageStatus.Processing;
            var deadLettered = (int)InboxMessageStatus.DeadLettered;
            var staleClaimCutoff = now.Subtract(claimTimeout);
            var abandoned = AbandonedClaimError;

            await Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE ""messaging"".""InboxMessages""
                SET ""Status"" = {deadLettered}, ""ClaimedAt"" = NULL, ""LastError"" = {abandoned}
                WHERE ""Status"" = {processing} AND ""ClaimedAt"" <= {staleClaimCutoff} AND ""Attempts"" >= {maxAttempts};",
                cancellationToken);

            return await InboxMessages.FromSqlInterpolated($@"
                UPDATE ""messaging"".""InboxMessages""
                SET ""Status"" = {processing}, ""ClaimedAt"" = {now}, ""Attempts"" = ""Attempts"" + 1
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

        public Task<int> ArchiveProcessedInboxAsync(
            DateTime processedBefore, int batchSize, CancellationToken cancellationToken = default)
        {
            var processed = (int)InboxMessageStatus.Processed;
            var archivedAt = DateTime.UtcNow;

            return Database.ExecuteSqlInterpolatedAsync($@"
                WITH moved AS (
                    DELETE FROM ""messaging"".""InboxMessages""
                    WHERE ""Id"" IN (
                        SELECT ""Id"" FROM ""messaging"".""InboxMessages""
                        WHERE ""Status"" = {processed} AND ""ProcessedAt"" < {processedBefore}
                        ORDER BY ""ProcessedAt""
                        LIMIT {batchSize}
                        FOR UPDATE SKIP LOCKED
                    )
                    RETURNING *
                )
                INSERT INTO ""messaging"".""InboxMessagesArchive"" (
                    ""Id"", ""SourceName"", ""Payload"", ""ReceivedAt"", ""Status"", ""Attempts"", ""ClaimedAt"",
                    ""NextAttemptAt"", ""ProcessedAt"", ""LastError"", ""IdempotencyKey"", ""Channel"", ""PayloadHash"",
                    ""CorrelationId"", ""TraceParent"", ""ArchivedAt"")
                SELECT ""Id"", ""SourceName"", ""Payload"", ""ReceivedAt"", ""Status"", ""Attempts"", ""ClaimedAt"",
                    ""NextAttemptAt"", ""ProcessedAt"", ""LastError"", ""IdempotencyKey"", ""Channel"", ""PayloadHash"",
                    ""CorrelationId"", ""TraceParent"", {archivedAt}
                FROM moved;", cancellationToken);
        }

        public Task<int> ArchiveProcessedOutboxAsync(
            DateTime processedBefore, int batchSize, CancellationToken cancellationToken = default)
        {
            var processed = (int)OutboxMessageStatus.Processed;
            var archivedAt = DateTime.UtcNow;

            return Database.ExecuteSqlInterpolatedAsync($@"
                WITH moved AS (
                    DELETE FROM ""messaging"".""OutboxMessages""
                    WHERE ""Id"" IN (
                        SELECT ""Id"" FROM ""messaging"".""OutboxMessages""
                        WHERE ""Status"" = {processed} AND ""ProcessedAt"" < {processedBefore}
                        ORDER BY ""ProcessedAt""
                        LIMIT {batchSize}
                        FOR UPDATE SKIP LOCKED
                    )
                    RETURNING *
                )
                INSERT INTO ""messaging"".""OutboxMessagesArchive"" (
                    ""Id"", ""Type"", ""Payload"", ""SchemaVersion"", ""OccurredAt"", ""Status"", ""Attempts"",
                    ""ClaimedAt"", ""NextAttemptAt"", ""ProcessedAt"", ""LastError"", ""TraceParent"", ""CorrelationId"", ""ArchivedAt"")
                SELECT ""Id"", ""Type"", ""Payload"", ""SchemaVersion"", ""OccurredAt"", ""Status"", ""Attempts"",
                    ""ClaimedAt"", ""NextAttemptAt"", ""ProcessedAt"", ""LastError"", ""TraceParent"", ""CorrelationId"", {archivedAt}
                FROM moved;", cancellationToken);
        }
    }
}