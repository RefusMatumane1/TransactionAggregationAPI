using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Messaging.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
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

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."OutboxMessages"
                    ("Id", "Type", "Payload", "OccurredAt", "Status", "Attempts", "ClaimedAt", "NextAttemptAt", "ProcessedAt", "LastError")
                VALUES
                    ({messageId}, 'StaleClaimReclaimTest', {EmptyJsonPayload}::jsonb, now() - interval '30 minutes', 1, 0, now() - interval '15 minutes', NULL, NULL, NULL)
                """);

            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

            claimed.Should().ContainSingle(m => m.Id.Value == messageId,
                "a message stuck in Processing past the claim timeout must be reclaimable, or a crashed dispatcher's work is lost forever");
        }

        [Fact]
        public async Task ClaimOutboxMessagesAsync_MessageProcessingWithinTimeout_IsNotReclaimed()
        {
            using var context = _fixture.CreateMessagingContext();
            var messageId = Guid.NewGuid();

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."OutboxMessages"
                    ("Id", "Type", "Payload", "OccurredAt", "Status", "Attempts", "ClaimedAt", "NextAttemptAt", "ProcessedAt", "LastError")
                VALUES
                    ({messageId}, 'StaleClaimReclaimTest', {EmptyJsonPayload}::jsonb, now() - interval '2 minutes', 1, 0, now() - interval '1 minute', NULL, NULL, NULL)
                """);

            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

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

            var claimed = await context.ClaimInboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

            claimed.Should().ContainSingle(m => m.Id.Value == messageId);
        }

        [Fact]
        public async Task ClaimOutboxMessagesAsync_PendingMessage_IsClaimedRegardlessOfAge()
        {
            using var context = _fixture.CreateMessagingContext();
            var message = OutboxMessage.Create("StaleClaimReclaimTest", "{}");
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync();

            var claimed = await context.ClaimOutboxMessagesAsync(batchSize: 50, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

            claimed.Should().ContainSingle(m => m.Id == message.Id);
        }

        [Fact]
        public async Task Claim_CountsAsAnAttempt_SoAMessageThatCrashesItsWorkerStillRunsOutOfAttempts()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            using (var seed = _fixture.CreateMessagingContext(database))
            {
                seed.InboxMessages.Add(InboxMessage.Create("crash-loop-source", "{}"));
                await seed.SaveChangesAsync();
            }

            using var context = _fixture.CreateMessagingContext(database);
            var claimed = await context.ClaimInboxMessagesAsync(batchSize: 10, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

            claimed.Should().ContainSingle().Which.Attempts.Should().Be(1);
        }

        [Fact]
        public async Task StaleClaim_WithNoAttemptsLeft_IsDeadLettered_NotReclaimedForever()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            using var context = _fixture.CreateMessagingContext(database);
            var messageId = Guid.NewGuid();

            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "messaging"."InboxMessages"
                    ("Id", "SourceName", "Payload", "ReceivedAt", "Status", "Attempts", "ClaimedAt")
                VALUES
                    ({messageId}, 'crash-loop-source', {EmptyJsonPayload}::jsonb, now() - interval '2 hours', 1, 5, now() - interval '15 minutes')
                """);

            var claimed = await context.ClaimInboxMessagesAsync(batchSize: 10, claimTimeout: TimeSpan.FromMinutes(10), maxAttempts: 5);

            claimed.Should().BeEmpty();
            var stored = await context.InboxMessages.AsNoTracking().SingleAsync(m => m.Id == InboxMessageId.CreateFrom(messageId));
            stored.Status.Should().Be(InboxMessageStatus.DeadLettered);
            stored.LastError.Should().Be(MessagingDbContext.AbandonedClaimError);
        }
    }
}