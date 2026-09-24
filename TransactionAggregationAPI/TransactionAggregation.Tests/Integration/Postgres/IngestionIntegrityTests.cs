using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Application.Contracts;
using Modules.WebhookSources.Contracts;
using Modules.WebhookSources.Domain;
using Npgsql;
using NSubstitute;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
using System.Data.Common;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Ingestion guarantees that only a real Postgres can prove: a source can't write into
    /// an institution it isn't authorized for, external ids are unique per institution (not
    /// per customer), and the outbox commits atomically with the rows it describes even when
    /// the retrying execution strategy has to re-run the transaction.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class IngestionIntegrityTests(PostgresContainerFixture fixture)
    {
        private static ITransactionCategorizationService Categorization()
        {
            var service = Substitute.For<ITransactionCategorizationService>();
            service.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
                .Returns(TransactionCategory.Uncategorized);
            return service;
        }

        private static ExternalTransactionDTO Dto(string id) => new()
        {
            Id = id,
            Amount = -42.125m,
            Currency = "KWD",
            Description = "Integrity test",
            Category = string.Empty,
            Date = DateTime.UtcNow
        };

        private async Task<(Customer Customer, string ExternalAccountId)> LinkAsync(
            Institution institution, Customer? existingCustomer = null)
        {
            using var customers = fixture.CreateCustomersContext();
            var customer = existingCustomer;
            if (customer is null)
            {
                customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Integrity Test User");
                customers.Customers.Add(customer);
            }

            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Integrity Account", AccountType.Checking, "ZAR");
            customers.Accounts.Add(account);
            await customers.SaveChangesAsync();

            using var bankLinks = fixture.CreateBankLinksContext();
            var externalAccountId = $"ext-{Guid.NewGuid():N}";
            var link = BankLink.Create(customer.Id, institution);
            link.Activate(account.Id, externalAccountId, "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            bankLinks.BankLinks.Add(link);
            await bankLinks.SaveChangesAsync();

            return (customer, externalAccountId);
        }

        private ProcessInboundTransactionsCommandHandler Handler(
            TransactionsDbContext context, MessagingDbContext messaging, IWebhookSourceDirectory directory) =>
            new(context, messaging, new BankLinksReadApi(fixture.CreateBankLinksContext()), directory,
                TestNormalizers.Neutral, Categorization(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

        [Fact]
        public async Task Handle_SourceNotAuthorizedForTheLinksInstitution_IsRefusedAsPermanent_AndStoresNothing()
        {
            var (customer, externalAccountId) = await LinkAsync(Institution.FNB);

            // A real, active source — but scoped to a different bank than the account's.
            var sourceName = $"absa-only-{Guid.NewGuid():N}";
            using (var sources = fixture.CreateWebhookSourcesContext())
            {
                sources.WebhookSources.Add(WebhookSource.Create(sourceName, ["Absa"]).Source);
                await sources.SaveChangesAsync();
            }

            using var messaging = fixture.CreateMessagingContext();
            using var context = fixture.CreateContext(messaging);
            var directory = new WebhookSourceDirectory(fixture.CreateWebhookSourcesContext());

            var result = await Handler(context, messaging, directory).Handle(
                new ProcessInboundTransactionsCommand(sourceName, externalAccountId, [Dto("forged-1")]),
                CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Forbidden,
                "an authorization refusal is permanent — the inbox must dead-letter it, not retry it");

            using var verify = fixture.CreateContext();
            (await verify.Transactions.CountAsync(t => t.CustomerId == customer.Id))
                .Should().Be(0, "a source must never write into an account at an institution it isn't authorized for");
        }

        [Fact]
        public async Task Handle_SourceAuthorizedForTheInstitution_StoresTheTransaction()
        {
            var (customer, externalAccountId) = await LinkAsync(Institution.Capitec);

            var sourceName = $"capitec-feed-{Guid.NewGuid():N}";
            using (var sources = fixture.CreateWebhookSourcesContext())
            {
                sources.WebhookSources.Add(WebhookSource.Create(sourceName, ["capitec"]).Source);
                await sources.SaveChangesAsync();
            }

            using var messaging = fixture.CreateMessagingContext();
            using var context = fixture.CreateContext(messaging);
            var directory = new WebhookSourceDirectory(fixture.CreateWebhookSourcesContext());

            var result = await Handler(context, messaging, directory).Handle(
                new ProcessInboundTransactionsCommand(sourceName, externalAccountId, [Dto("ok-1")]),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue("institution names match case-insensitively");

            using var verify = fixture.CreateContext();
            var stored = await verify.Transactions.SingleAsync(t => t.CustomerId == customer.Id);
            stored.Amount.Amount.Should().Be(-42.125m, "3-decimal currencies (KWD, BHD, JOD) must not be rounded to 2 places");
        }

        [Fact]
        public async Task Handle_SameExternalIdFromTwoInstitutions_ForOneCustomer_StoresBoth()
        {
            var (customer, fnbAccount) = await LinkAsync(Institution.FNB);
            var (_, absaAccount) = await LinkAsync(Institution.Absa, customer);
            var sharedId = $"txn-{Guid.NewGuid():N}";

            foreach (var externalAccountId in new[] { fnbAccount, absaAccount })
            {
                using var messaging = fixture.CreateMessagingContext();
                using var context = fixture.CreateContext(messaging);
                var result = await Handler(context, messaging, TestInstitutions.AllowAllDirectory()).Handle(
                    new ProcessInboundTransactionsCommand("multi-bank", externalAccountId, [Dto(sharedId)]),
                    CancellationToken.None);

                result.IsSuccess.Should().BeTrue();
                result.Value.Should().Be(1, "an id from a different bank is a different transaction, not a duplicate");
            }

            using var verify = fixture.CreateContext();
            (await verify.Transactions.CountAsync(t => t.CustomerId == customer.Id && t.Source.ExternalId == sharedId))
                .Should().Be(2);
        }

        /// <summary>
        /// Before the fix, the first attempt's transaction rows were accepted by the change
        /// tracker as soon as they were written; the commit then failed transiently, the
        /// execution strategy re-ran the transaction, and the retry committed the outbox rows
        /// without the transaction they announce — the ingested transaction was lost while the
        /// inbox message was marked processed.
        /// </summary>
        [Fact]
        public async Task SaveChanges_TransientFailureOnCommit_RetriesAndCommitsRowsAndOutboxTogether()
        {
            var (customer, externalAccountId) = await LinkAsync(Institution.StandardBank);

            using var messaging = fixture.CreateMessagingContext();
            var failOnce = new FailFirstCommitInterceptor();
            var contextOptions = new DbContextOptionsBuilder<TransactionsDbContext>()
                .UseNpgsql((NpgsqlConnection)messaging.Database.GetDbConnection(), npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(50), errorCodesToAdd: [FailFirstCommitInterceptor.SqlState]))
                .AddInterceptors(failOnce)
                .Options;
            using var context = new TransactionsDbContext(contextOptions, Substitute.For<IMediator>(), messaging);

            var externalId = $"atomic-{Guid.NewGuid():N}";
            var result = await Handler(context, messaging, TestInstitutions.AllowAllDirectory()).Handle(
                new ProcessInboundTransactionsCommand("atomicity", externalAccountId, [Dto(externalId)]),
                CancellationToken.None);

            failOnce.Failures.Should().Be(1, "the test is only meaningful if the fault actually fired");
            result.IsSuccess.Should().BeTrue();

            using var verify = fixture.CreateContext();
            var stored = await verify.Transactions.SingleOrDefaultAsync(
                t => t.CustomerId == customer.Id && t.Source.ExternalId == externalId);
            stored.Should().NotBeNull("the retry must re-insert the transaction, not just the outbox rows announcing it");

            using var verifyMessaging = fixture.CreateMessagingContext();
            var synced = (await verifyMessaging.OutboxMessages
                    .Where(m => m.Type == OutboxMessageTypes.TransactionSynced)
                    .ToListAsync())
                .Select(m => JsonSerializer.Deserialize<TransactionSyncedOutboxPayload>(m.Payload)!)
                .Where(p => p.TransactionId == stored!.Id.Value)
                .ToList();
            synced.Should().ContainSingle("exactly one announcement per committed transaction");
        }

        /// <summary>Throws a retryable Postgres error in place of the first commit, before it reaches the server.</summary>
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