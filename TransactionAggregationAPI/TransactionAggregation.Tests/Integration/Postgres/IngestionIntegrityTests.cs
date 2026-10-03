using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Contracts.IntegrationEvents;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using Npgsql;
using NSubstitute;
using SharedKernel.Common.Enums;
using System.Data.Common;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class IngestionIntegrityTests(PostgresContainerFixture fixture)
    {
        private static ITransactionCategorizationService Categorization()
        {
            var service = Substitute.For<ITransactionCategorizationService>();
            service.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(TransactionCategory.Uncategorized);
            return service;
        }

        private static ExternalTransactionDTO Dto(string id) => new()
        {
            Id = id,
            Amount = -42.125m,
            Currency = "ZAR",
            Description = "Integrity test",
            Category = string.Empty,
            Date = DateTime.UtcNow
        };

        private static string NewAccount() => $"ext-{Guid.NewGuid():N}";

        private ProcessInboundTransactionsCommandHandler Handler(TransactionsDbContext context, MessagingDbContext messaging) =>
            new(context, messaging, TestNormalizers.Neutral, Categorization(),
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

        [Fact]
        public async Task Handle_DeliveryNamingAnotherBank_IsRefusedAsPermanent_AndStoresNothing()
        {
            var externalAccountId = NewAccount();

            using var messaging = fixture.CreateMessagingContext();
            using var context = fixture.CreateContext(messaging);

            var result = await Handler(context, messaging).Handle(
                new ProcessInboundTransactionsCommand(TestInstitutions.FNB, externalAccountId, TestInstitutions.Absa, [Dto("forged-1")]),
                CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Forbidden,
                "an authorization refusal is permanent — the inbox must dead-letter it, not retry it");

            using var verify = fixture.CreateContext();
            (await verify.Transactions.CountAsync(t => t.ExternalAccountId == externalAccountId))
                .Should().Be(0, "a bank must never write transactions for another bank");
        }

        [Fact]
        public async Task Handle_DeliveryWithoutAnInstitution_IsStoredUnderTheSourceBank()
        {
            var externalAccountId = NewAccount();

            using var messaging = fixture.CreateMessagingContext();
            using var context = fixture.CreateContext(messaging);

            var result = await Handler(context, messaging).Handle(
                new ProcessInboundTransactionsCommand(TestInstitutions.Capitec, externalAccountId, null, [Dto("ok-1")]),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();

            using var verify = fixture.CreateContext();
            var stored = await verify.Transactions.SingleAsync(t => t.ExternalAccountId == externalAccountId);
            stored.Source.Name.Should().Be(TestInstitutions.Capitec);
            stored.Amount.Amount.Should().Be(-42.125m, "3-decimal currencies (KWD, BHD, JOD) must not be rounded to 2 places");
        }

        [Fact]
        public async Task Handle_SameExternalIdFromTwoBanks_StoresBoth()
        {
            var externalAccountId = NewAccount();
            var sharedId = $"txn-{Guid.NewGuid():N}";

            foreach (var bank in new[] { TestInstitutions.FNB, TestInstitutions.Absa })
            {
                using var messaging = fixture.CreateMessagingContext();
                using var context = fixture.CreateContext(messaging);
                var result = await Handler(context, messaging).Handle(
                    new ProcessInboundTransactionsCommand(bank, externalAccountId, null, [Dto(sharedId)]),
                    CancellationToken.None);

                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(1, "an id from a different bank is a different transaction, not a duplicate");
            }

            using var verify = fixture.CreateContext();
            (await verify.Transactions.CountAsync(t => t.ExternalAccountId == externalAccountId && t.Source.ExternalId == sharedId))
                .Should().Be(2);
        }

        [Fact]
        public async Task SaveChanges_TransientFailureOnCommit_RetriesAndCommitsRowsAndOutboxTogether()
        {
            var externalAccountId = NewAccount();

            using var messaging = fixture.CreateMessagingContext();
            var failOnce = new FailFirstCommitInterceptor();
            var contextOptions = new DbContextOptionsBuilder<TransactionsDbContext>()
                .UseNpgsql((NpgsqlConnection)messaging.Database.GetDbConnection(), npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(50), errorCodesToAdd: [FailFirstCommitInterceptor.SqlState]))
                .AddInterceptors(failOnce)
                .Options;
            using var context = new TransactionsDbContext(contextOptions, messaging, fixture.CreateAuditTrail(messaging));

            var externalId = $"atomic-{Guid.NewGuid():N}";
            var result = await Handler(context, messaging).Handle(
                new ProcessInboundTransactionsCommand("atomicity", externalAccountId, null, [Dto(externalId)]),
                CancellationToken.None);

            failOnce.Failures.Should().Be(1, "the test is only meaningful if the fault actually fired");
            result.IsSuccess.Should().BeTrue();

            using var verify = fixture.CreateContext();
            var stored = await verify.Transactions.SingleOrDefaultAsync(
                t => t.ExternalAccountId == externalAccountId && t.Source.ExternalId == externalId);
            stored.Should().NotBeNull("the retry must re-insert the transaction, not just the outbox rows announcing it");

            using var verifyMessaging = fixture.CreateMessagingContext();
            var recorded = (await verifyMessaging.OutboxMessages
                    .Where(m => m.Type == OutboxMessageTypes.TransactionRecorded)
                    .ToListAsync())
                .Select(m => JsonSerializer.Deserialize<TransactionRecorded>(m.Payload)!)
                .Where(p => p.TransactionId == stored!.Id.Value)
                .ToList();
            recorded.Should().ContainSingle("exactly one announcement per committed transaction");

            using var verifyAudit = fixture.CreateAuditContext();
            (await verifyAudit.AuditEvents.CountAsync(e => e.TransactionId == stored!.Id.Value
                                                           && e.EventType == AuditEventTypes.TransactionIngested))
                .Should().Be(1, "the audit row commits with the transaction, once, even when the commit is retried");
        }

        private sealed class FailFirstCommitInterceptor : DbTransactionInterceptor
        {
            public const string SqlState = "40001";

            public int Failures { get; private set; }

            public override ValueTask<InterceptionResult> TransactionCommittingAsync(
                DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
                CancellationToken cancellationToken = default)
            {
                if (Failures == 0)
                {
                    Failures++;
                    throw new PostgresException("injected transient commit failure", "ERROR", "ERROR", SqlState);
                }

                return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
            }
        }
    }
}