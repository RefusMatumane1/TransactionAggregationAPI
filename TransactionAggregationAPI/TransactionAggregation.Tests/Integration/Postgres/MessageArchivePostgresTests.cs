using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Npgsql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class MessageArchivePostgresTests(PostgresContainerFixture fixture)
    {
        private static readonly DateTime Cutoff = DateTime.UtcNow.AddDays(-30);

        [Fact]
        public async Task Archive_MovesOnlyOldProcessedRows_Intact_AndLeavesTheRestInTheQueue()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            var oldProcessed = await ReceiveAsync(database, "old-processed");
            var recentProcessed = await ReceiveAsync(database, "recent-processed");
            var oldDeadLettered = await ReceiveAsync(database, "old-dead");
            var pending = await ReceiveAsync(database, "pending");
            await SetStateAsync(database, oldProcessed, InboxMessageStatus.Processed, DateTime.UtcNow.AddDays(-40));
            await SetStateAsync(database, recentProcessed, InboxMessageStatus.Processed, DateTime.UtcNow.AddDays(-1));
            await SetStateAsync(database, oldDeadLettered, InboxMessageStatus.DeadLettered, DateTime.UtcNow.AddDays(-40));

            string originalPayload;
            using (var before = fixture.CreateMessagingContext(database))
                originalPayload = (await before.InboxMessages.AsNoTracking().SingleAsync(m => m.Id == InboxId(oldProcessed))).Payload;

            using (var archiver = fixture.CreateMessagingContext(database))
                (await archiver.ArchiveProcessedInboxAsync(Cutoff, batchSize: 100)).Should().Be(1);

            using var verify = fixture.CreateMessagingContext(database);
            (await verify.InboxMessages.Select(m => m.Id.Value).ToListAsync())
                .Should().BeEquivalentTo([recentProcessed, oldDeadLettered, pending]);
            var archived = await verify.ArchivedInboxMessages.SingleAsync();
            archived.Id.Should().Be(oldProcessed);
            archived.Payload.Should().Be(originalPayload);
            archived.IdempotencyKey.Should().Be("old-processed");
            archived.Status.Should().Be(InboxMessageStatus.Processed);
        }

        [Fact]
        public async Task Archive_RunConcurrently_NeitherLosesNorDuplicatesARow()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            var ids = new List<Guid>();
            for (var i = 0; i < 60; i++)
            {
                var id = await ReceiveAsync(database, $"bulk-{i}");
                await SetStateAsync(database, id, InboxMessageStatus.Processed, DateTime.UtcNow.AddDays(-40));
                ids.Add(id);
            }

            async Task<int> ArchiveAllAsync()
            {
                var total = 0;
                int moved;
                do
                {
                    using var archiver = fixture.CreateMessagingContext(database);
                    moved = await archiver.ArchiveProcessedInboxAsync(Cutoff, batchSize: 7);
                    total += moved;
                }
                while (moved > 0);
                return total;
            }

            var counts = await Task.WhenAll(ArchiveAllAsync(), ArchiveAllAsync(), ArchiveAllAsync());

            counts.Sum().Should().Be(60);
            using var verify = fixture.CreateMessagingContext(database);
            (await verify.InboxMessages.CountAsync()).Should().Be(0);
            (await verify.ArchivedInboxMessages.Select(m => m.Id).ToListAsync()).Should().BeEquivalentTo(ids);
        }

        [Fact]
        public async Task Outbox_ArchivesProcessedRows()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            using (var messaging = fixture.CreateMessagingContext(database))
            {
                var done = OutboxMessage.Create("TransactionSynced", "{}");
                done.MarkProcessed();
                messaging.OutboxMessages.AddRange(done, OutboxMessage.Create("TransactionSynced", "{}"));
                await messaging.SaveChangesAsync();
                await messaging.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE messaging.\"OutboxMessages\" SET \"ProcessedAt\" = {DateTime.UtcNow.AddDays(-40)} WHERE \"Id\" = {done.Id.Value}");
            }

            using var archiver = fixture.CreateMessagingContext(database);
            (await archiver.ArchiveProcessedOutboxAsync(Cutoff, batchSize: 100)).Should().Be(1);
            (await archiver.OutboxMessages.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task Idempotency_StillHoldsForArchivedDeliveries()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            var original = await ReceiveAsync(database, "archived-key");
            await SetStateAsync(database, original, InboxMessageStatus.Processed, DateTime.UtcNow.AddDays(-40));
            using (var archiver = fixture.CreateMessagingContext(database))
                await archiver.ArchiveProcessedInboxAsync(Cutoff, batchSize: 100);

            using var messaging = fixture.CreateMessagingContext(database);
            var handler = new ReceiveBankTransactionsCommandHandler(messaging, fixture.CreateRetryingContext(messaging), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance);

            var replay = await handler.Handle(Command("archived-key"), CancellationToken.None);
            var reused = await handler.Handle(Command("archived-key", amount: -99m), CancellationToken.None);

            replay.IsSuccess.Should().BeTrue();
            replay.Value.IsDuplicate.Should().BeTrue();
            replay.Value.InboxMessageId.Should().Be(original);
            reused.IsFailure.Should().BeTrue();
            reused.Error.Code.Should().Be(InboxErrors.IdempotencyKeyReusedCode);
            (await messaging.InboxMessages.CountAsync(m => m.IdempotencyKey == "archived-key")).Should().Be(0);
        }

        private static ReceiveBankTransactionsCommand Command(string key, decimal amount = -10m) =>
            new("archive-source", "ext-acc-1", null,
            [
                new ExternalTransactionDTO
                {
                    Id = $"txn-{key}", Amount = amount, Currency = "ZAR", Description = "Archive test",
                    Category = string.Empty, Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ], key);

        private async Task<Guid> ReceiveAsync(string database, string key)
        {
            using var messaging = fixture.CreateMessagingContext(database);
            var result = await new ReceiveBankTransactionsCommandHandler(messaging, fixture.CreateRetryingContext(messaging), NullLogger<ReceiveBankTransactionsCommandHandler>.Instance)
                .Handle(Command(key), CancellationToken.None);
            return result.Value.InboxMessageId;
        }

        private static async Task SetStateAsync(string database, Guid id, InboxMessageStatus status, DateTime processedAt)
        {
            await using var connection = new NpgsqlConnection(database);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "UPDATE messaging.\"InboxMessages\" SET \"Status\" = @status, \"ProcessedAt\" = @at WHERE \"Id\" = @id", connection);
            command.Parameters.AddWithValue("status", (int)status);
            command.Parameters.AddWithValue("at", processedAt);
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }

        private static BuildingBlocks.Messaging.ValueObjects.InboxMessageId InboxId(Guid id) =>
            BuildingBlocks.Messaging.ValueObjects.InboxMessageId.CreateFrom(id);
    }
}