using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Automates the manual verification performed during this review: a message
    /// stuck in Processing status (simulating a dispatcher that claimed it, then
    /// crashed before the final SaveChangesAsync) was inserted by hand via
    /// `docker exec psql`, and a live OutboxDispatcherBackgroundService run was
    /// watched to confirm it got reclaimed. This is the permanent, CI-enforced
    /// version of that check — see docs/failure-scenarios.md scenarios 10/11/13
    /// and ADR discussion of the claim SQL (FOR UPDATE SKIP LOCKED), which the
    /// in-memory EF Core provider used everywhere else in this suite cannot run.
    /// Inbox/Outbox now live in the "messaging" schema — see
    /// docs/adr/0009-schema-per-module-database-strategy.md.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class StaleClaimReclaimTests
    {
        private readonly PostgresContainerFixture _fixture;
        private const string EmptyJsonPayload = "{}";

        public StaleClaimReclaimTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ClaimOutboxMessagesAsync_MessageStuckInProcessingPastTimeout_IsReclaimed()
        {
            using var context = _fixture.CreateMessagingContext();
            var messageId = Guid.NewGuid();

            // Simulates a dispatcher that claimed this message, then crashed before
            // the final SaveChangesAsync() ever marked it Processed/Failed.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."OutboxMessages"
                    ("Id", "Type", "Payload", "OccurredAt", "Status", "Attempts", "ClaimedAt", "NextAttemptAt", "ProcessedAt", "LastError")
                VALUES
                    ({messageId}, 'StaleClaimReclaimTest', {EmptyJsonPayload}::jsonb, now() - interval '30 minutes', 1, 0, now() - interval '15 minutes', NULL, NULL, NULL)
                """);

            // Default ClaimTimeoutMinutes is 10 — 15 minutes stale is past it.
            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10));

            claimed.Should().ContainSingle(m => m.Id.Value == messageId,
                "a message stuck in Processing past the claim timeout must be reclaimable, or a crashed dispatcher's work is lost forever");
        }

        [Fact]
        public async Task ClaimOutboxMessagesAsync_MessageProcessingWithinTimeout_IsNotReclaimed()
        {
            using var context = _fixture.CreateMessagingContext();
            var messageId = Guid.NewGuid();

            // A different live dispatcher instance could still be actively working
            // this message — reclaiming it too early would cause double-processing.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."OutboxMessages"
                    ("Id", "Type", "Payload", "OccurredAt", "Status", "Attempts", "ClaimedAt", "NextAttemptAt", "ProcessedAt", "LastError")
                VALUES
                    ({messageId}, 'StaleClaimReclaimTest', {EmptyJsonPayload}::jsonb, now() - interval '2 minutes', 1, 0, now() - interval '1 minute', NULL, NULL, NULL)
                """);

            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10));

            claimed.Should().NotContain(m => m.Id.Value == messageId,
                "a message still within its claim timeout might be actively processed by another dispatcher instance");
        }

        [Fact]
        public async Task ClaimInboxMessagesAsync_MessageStuckInProcessingPastTimeout_IsReclaimed()
        {
            using var context = _fixture.CreateMessagingContext();
            var messageId = Guid.NewGuid();

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."InboxMessages"
                    ("Id", "SourceName", "Payload", "ReceivedAt", "Status", "Attempts", "ClaimedAt", "NextAttemptAt", "ProcessedAt", "LastError")
                VALUES
                    ({messageId}, 'stale-claim-test-source', {EmptyJsonPayload}::jsonb, now() - interval '30 minutes', 1, 0, now() - interval '15 minutes', NULL, NULL, NULL)
                """);

            var claimed = await context.ClaimInboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10));

            claimed.Should().ContainSingle(m => m.Id.Value == messageId);
        }

        [Fact]
        public async Task ClaimOutboxMessagesAsync_PendingMessage_IsClaimedRegardlessOfAge()
        {
            using var context = _fixture.CreateMessagingContext();
            var message = OutboxMessage.Create("StaleClaimReclaimTest", "{}");
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();

            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10));

            claimed.Should().ContainSingle(m => m.Id == message.Id);
        }
    }
}
