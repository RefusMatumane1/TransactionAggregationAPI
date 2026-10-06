using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Infrastructure.Migrations;
using Npgsql;
using NSubstitute;
using SharedKernel.Common.Models;
using System.Text.Json;
using TransactionAggregation.Worker.BackgroundServices;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class ProcessingGuaranteesPostgresTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ProcessingGuaranteesPostgresTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task HandlerCommit_MarksTheInboxMessageProcessed_InTheSameTransaction_SoACrashLeavesNothingToReclaim()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            var externalAccountId = $"atomic-acc-{Guid.NewGuid():N}";
            var inboxId = await EnqueueAsync(database, externalAccountId, [Transaction("atomic-t1", -42.10m), Transaction("atomic-t2", -7m)]);

            // Process, then crash: the batch save never runs.
            await using (var messaging = _fixture.CreateMessagingContext(database))
            {
                var claimed = await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5);
                claimed.Should().ContainSingle(m => m.Id.Value == inboxId);

                await using var context = _fixture.CreateContext(messaging, database);
                await Dispatcher().ProcessMessageAsync(claimed[0], SenderFor(context, messaging, database), messaging, context, CancellationToken.None);
            }

            (await StatusOfAsync(database, inboxId)).Should().Be(InboxMessageStatus.Processed,
                "the processed mark commits with the transactions, not in a later save a crash could lose");

            await ExecuteAsync(database, $"UPDATE messaging.\"InboxMessages\" SET \"ClaimedAt\" = now() - interval '1 hour' WHERE \"Id\" = '{inboxId}'");
            await using (var messaging = _fixture.CreateMessagingContext(database))
                (await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5)).Should().BeEmpty(
                    "nothing is left to re-claim, so no duplicate audit events or alerts are emitted");

            await using var verify = _fixture.CreateContext(connectionString: database);
            (await verify.Transactions.CountAsync(t => t.ExternalAccountId == externalAccountId)).Should().Be(2);
        }

        [Fact]
        public async Task CrashBeforeCommit_TheClaimGoesStale_AndTheMessageIsProcessedExactlyOnce()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            var externalAccountId = $"stale-acc-{Guid.NewGuid():N}";
            var inboxId = await EnqueueAsync(database, externalAccountId, [Transaction("stale-t1", -5m)]);

            // Claimed, then the worker dies.
            await using (var messaging = _fixture.CreateMessagingContext(database))
                (await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5)).Should().ContainSingle();
            (await StatusOfAsync(database, inboxId)).Should().Be(InboxMessageStatus.Processing);

            await ExecuteAsync(database, $"UPDATE messaging.\"InboxMessages\" SET \"ClaimedAt\" = now() - interval '1 hour' WHERE \"Id\" = '{inboxId}'");
            await using (var messaging = _fixture.CreateMessagingContext(database))
            {
                var reclaimed = await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5);
                reclaimed.Should().ContainSingle(m => m.Id.Value == inboxId, "a stale claim is recoverable, not stuck forever");

                await using var context = _fixture.CreateContext(messaging, database);
                await Dispatcher().ProcessMessageAsync(reclaimed[0], SenderFor(context, messaging, database), messaging, context, CancellationToken.None);
            }

            (await StatusOfAsync(database, inboxId)).Should().Be(InboxMessageStatus.Processed);
            await using var verify = _fixture.CreateContext(connectionString: database);
            (await verify.Transactions.CountAsync(t => t.ExternalAccountId == externalAccountId)).Should().Be(1);
        }

        [Fact]
        public async Task CrashAfterCommit_BeforeAck_RedeliveryIsAbsorbed_WithoutDuplicateTransactions()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            var externalAccountId = $"crash-acc-{Guid.NewGuid():N}";
            IReadOnlyList<ExternalTransactionDTO> batch = [Transaction("crash-t1", -42.10m), Transaction("crash-t2", -7m)];

            // Committed but not acknowledged, so the bank redelivers with a new idempotency key:
            // only the transaction key stands in the way.
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var delivery = await EnqueueAsync(database, externalAccountId, batch);
                await using var messaging = _fixture.CreateMessagingContext(database);
                var claimed = await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5);
                await using var context = _fixture.CreateContext(messaging, database);
                foreach (var message in claimed)
                    await Dispatcher().ProcessMessageAsync(message, SenderFor(context, messaging, database), messaging, context, CancellationToken.None);
                await context.SaveChangesAsync();
                (await StatusOfAsync(database, delivery)).Should().Be(InboxMessageStatus.Processed);
            }

            await using var verify = _fixture.CreateContext(connectionString: database);
            var rows = await verify.Transactions.Where(t => t.ExternalAccountId == externalAccountId).ToListAsync();
            rows.Should().HaveCount(2, "the second delivery must not create a second copy of either transaction");
            rows.Select(t => t.Source.ExternalId).Should().BeEquivalentTo(["crash-t1", "crash-t2"]);
        }

        [Fact]
        public async Task HandlerFailure_LeavesTheMessageRetryable_NeverMarkedProcessed()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            var inboxId = await EnqueueAsync(database, $"conflict-acc-{Guid.NewGuid():N}", [Transaction("conflict-t1", -1m)]);

            // Transient failures retry; they are not dead-lettered.
            var conflicted = Substitute.For<ISender>();
            conflicted.Send(Arg.Any<ProcessInboundTransactionsCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<int>(Error.Conflict("Concurrent ingestion kept conflicting")));

            await using (var messaging = _fixture.CreateMessagingContext(database))
            {
                var claimed = await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5);
                await using var context = _fixture.CreateContext(messaging, database);
                await Dispatcher().ProcessMessageAsync(claimed.Single(), conflicted, messaging, context, CancellationToken.None);
                await context.SaveChangesAsync();
            }

            await using var check = _fixture.CreateMessagingContext(database);
            var stored = await check.InboxMessages.AsNoTracking()
                .SingleAsync(m => m.Id == BuildingBlocks.Messaging.ValueObjects.InboxMessageId.CreateFrom(inboxId));
            stored.Status.Should().Be(InboxMessageStatus.Pending, "a transient failure is retried with backoff");
            stored.ProcessedAt.Should().BeNull("the early processed mark must not survive a failure");

            await using var audit = _fixture.CreateAuditContext(database);
            (await audit.AuditEvents.CountAsync(e => e.InboxMessageId == inboxId && e.EventType == AuditEventTypes.InboundProcessingFailed))
                .Should().Be(1, "the failure's audit row commits with the retry state it describes");
        }

        [Fact]
        public async Task DataTheStoreCanNeverAccept_IsDeadLetteredOnTheFirstAttempt_WithAnActionableReason()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            var externalAccountId = $"poison-acc-{Guid.NewGuid():N}";

            var inboxId = await EnqueueAsync(database, externalAccountId, [Transaction(new string('x', 150), -10m)]);

            await using (var messaging = _fixture.CreateMessagingContext(database))
            {
                var claimed = await messaging.ClaimInboxMessagesAsync(10, TimeSpan.FromMinutes(10), maxAttempts: 5);
                await using var context = _fixture.CreateContext(messaging, database);
                await Dispatcher().ProcessMessageAsync(claimed.Single(), SenderFor(context, messaging, database), messaging, context, CancellationToken.None);
                await context.SaveChangesAsync();
            }

            await using var check = _fixture.CreateMessagingContext(database);
            var message = await check.InboxMessages.AsNoTracking().SingleAsync(m => m.Id == BuildingBlocks.Messaging.ValueObjects.InboxMessageId.CreateFrom(inboxId));
            message.Status.Should().Be(InboxMessageStatus.DeadLettered, "retrying cannot change the outcome");
            message.Attempts.Should().Be(1, "no retry budget is spent on a permanent failure");
            message.LastError.Should().Contain("External ID must be 1-100 characters");
        }

        [Theory]
        [InlineData("0", "'ZAR'", "4", "1", "CK_Transactions_Amount_NonZero")]
        [InlineData("-1", "'zar'", "4", "1", "CK_Transactions_Currency_Iso4217")]
        [InlineData("-1", "'ZAR'", "99", "1", "CK_Transactions_Status_Defined")]
        [InlineData("-1", "'ZAR'", "4", "99", "CK_Transactions_Category_Defined")]
        public async Task DatabaseConstraints_RejectInvalidRows_WhateverTheWritePath(
            string amount, string currency, string status, string category, string constraint)
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();

            var act = () => ExecuteAsync(database, $"""
                INSERT INTO transactions."Transactions"
                    ("Id", "ExternalAccountId", "Amount", "Currency", "Description", "Category", "SourceName",
                     "SourceExternalId", "Status", "Date", "CreatedAt", "Metadata")
                VALUES (gen_random_uuid(), 'constraint-test', {amount}, {currency}, 'x', {category}, 'FNB',
                        'probe', {status}, now(), now(), jsonb_build_object())
                """);

            (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(constraint);
        }

        // Every ledger query shape against 50 000 rows (10% pre-ledger): each must use its partial index.
        [Fact]
        public async Task LedgerQueries_UseTheirIndexes_AndStayFastAtVolume()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            for (var i = 0; i < 200; i++)
                await ExecuteAsync(database, InsertTransactionsSql($"bulk-acc-{i}", 250, $"bulk-{i}"));
            // A rare term: a common one is cheaper to find via the date index.
            await ExecuteAsync(database, """
                INSERT INTO transactions."Transactions"
                    ("Id", "ExternalAccountId", "Amount", "Currency", "Description", "Category", "SourceName",
                     "SourceExternalId", "Status", "Date", "CreatedAt", "UpdatedAt", "Metadata")
                VALUES (gen_random_uuid(), 'needle-acc', -42, 'ZAR', 'NEEDLEWORK STUDIO ROSEBANK', 10, 'FNB',
                        'needle-1', 4, now() - interval '3 days', now(), NULL, jsonb_build_object());
                """);
            await ExecuteAsync(database, "ANALYZE transactions.\"Transactions\"");

            const string Ledger = "SELECT * FROM transactions.\"Transactions\" WHERE \"Status\" = 4";
            var history = await ExplainAsync(database, $"{Ledger} ORDER BY \"Date\" DESC, \"Id\" DESC LIMIT 21");
            var deepPage = await ExplainAsync(database,
                $"{Ledger} AND (\"Date\", \"Id\") < (now() - interval '200 hours', '{Guid.Empty}') ORDER BY \"Date\" DESC, \"Id\" DESC LIMIT 21");
            var byAmount = await ExplainAsync(database, $"{Ledger} ORDER BY \"Amount\", \"Id\" LIMIT 21");
            var statement = await ExplainAsync(database,
                $"{Ledger} AND \"SourceName\" = 'FNB' AND \"ExternalAccountId\" = 'bulk-acc-7' ORDER BY \"Date\" DESC, \"Id\" DESC LIMIT 21");
            var search = await ExplainAsync(database,
                $"{Ledger} AND \"Description\" ILIKE '%needlework stu%' ORDER BY \"Date\" DESC, \"Id\" DESC LIMIT 21");

            history.Plan.Should().Contain(MakeLedgerInsertOnly.DateCoveringIndex).And.NotContain("Seq Scan").And.NotContain("\"Sort\"");
            deepPage.Plan.Should().Contain(MakeLedgerInsertOnly.DateCoveringIndex).And.NotContain("Seq Scan").And.NotContain("\"Sort\"");
            byAmount.Plan.Should().Contain(MakeLedgerInsertOnly.AmountIndex).And.NotContain("Seq Scan").And.NotContain("\"Sort\"");
            statement.Plan.Should().Contain(MakeLedgerInsertOnly.AccountIndex).And.NotContain("Seq Scan").And.NotContain("\"Sort\"");
            search.Plan.Should().Contain(MakeLedgerInsertOnly.DescriptionSearchIndex).And.NotContain("Seq Scan");

            foreach (var plan in new[] { history, deepPage, byAmount, statement, search })
                plan.ExecutionMs.Should().BeLessThan(50, "an index serves the query however large the table grows");
        }

        [Fact]
        public async Task TheLedger_RefusesUpdateDeleteAndTruncate_WhoeverAsks()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();
            await ExecuteAsync(database, InsertTransactionsSql("immutable-acc", count: 1, externalIdPrefix: "immutable"));

            foreach (var sql in new[]
                     {
                         "UPDATE transactions.\"Transactions\" SET \"Amount\" = 1",
                         "DELETE FROM transactions.\"Transactions\"",
                         "TRUNCATE transactions.\"Transactions\""
                     })
            {
                var act = () => ExecuteAsync(database, sql);
                (await act.Should().ThrowAsync<PostgresException>(sql)).Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
            }
        }

        private static ExternalTransactionDTO Transaction(string id, decimal amount) => new()
        {
            Id = id,
            Amount = amount,
            Currency = "ZAR",
            Description = "Card purchase",
            Category = string.Empty,
            Date = DateTime.UtcNow.AddDays(-1)
        };

        private static InboxDispatcherBackgroundService Dispatcher() =>
            new(Substitute.For<IServiceScopeFactory>(), NullLogger<InboxDispatcherBackgroundService>.Instance,
                Options.Create(new InboxOptions()));

        private ISender SenderFor(
            Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext context, MessagingDbContext messaging, string database)
        {
            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<ProcessInboundTransactionsCommand>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var handler = IngestionHandlerFactory.Create(context, messaging);
                    return handler.Handle(call.Arg<ProcessInboundTransactionsCommand>(), call.Arg<CancellationToken>());
                });
            return sender;
        }

        private async Task<Guid> EnqueueAsync(string database, string externalAccountId, IReadOnlyList<ExternalTransactionDTO> transactions)
        {
            await using var messaging = _fixture.CreateMessagingContext(database);
            var message = InboxMessage.Create("pg-test", JsonSerializer.Serialize(new InboundTransactionsPayload(externalAccountId, null, transactions)),
                idempotencyKey: Guid.NewGuid().ToString(), channel: "webhook");
            messaging.InboxMessages.Add(message);
            await messaging.SaveChangesAsync();
            return message.Id.Value;
        }

        private async Task<InboxMessageStatus> StatusOfAsync(string database, Guid inboxId)
        {
            await using var messaging = _fixture.CreateMessagingContext(database);
            return await messaging.InboxMessages.AsNoTracking()
                .Where(m => m.Id == BuildingBlocks.Messaging.ValueObjects.InboxMessageId.CreateFrom(inboxId))
                .Select(m => m.Status)
                .SingleAsync();
        }

        private static string InsertTransactionsSql(string account, int count, string externalIdPrefix) => $"""
            INSERT INTO transactions."Transactions"
                ("Id", "ExternalAccountId", "Amount", "Currency", "Description", "Category", "SourceName",
                 "SourceExternalId", "Status", "Date", "CreatedAt", "UpdatedAt", "Metadata")
            SELECT gen_random_uuid(), '{account}', -(n % 500 + 1), 'ZAR', 'Seeded ' || n, n % 12, 'FNB',
                   '{externalIdPrefix}-' || n, CASE WHEN n % 10 = 0 THEN 0 ELSE 4 END,
                   now() - (n || ' hours')::interval, now() - (n || ' hours')::interval, NULL, jsonb_build_object()
            FROM generate_series(1, {count}) AS n;
            """;

        private static async Task ExecuteAsync(string database, string sql)
        {
            await using var connection = new NpgsqlConnection(database);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<(string Plan, double ExecutionMs)> ExplainAsync(string database, string sql)
        {
            await using var connection = new NpgsqlConnection(database);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, FORMAT JSON) {sql}", connection);
            var json = (string)(await command.ExecuteScalarAsync())!;
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement[0];
            return (root.GetProperty("Plan").ToString(), root.GetProperty("Execution Time").GetDouble());
        }
    }
}