using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using NSubstitute;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// The exact race Instructions.md section 6 describes:
    ///   Request A -> check transaction -> not found
    ///   Request B -> check transaction -> not found
    ///   Request A -> insert
    ///   Request B -> insert
    /// This only proves anything against a real database — the in-memory EF Core
    /// provider used by ProcessInboundTransactionsCommandHandlerTests doesn't
    /// enforce the unique constraint the same way real Postgres does, and the
    /// handler's unique-violation retry (DbUpdateException.IsUniqueViolation, a
    /// Postgres-specific SQLSTATE) is something no in-memory-provider test could ever
    /// trigger. These tests are the only ones in the suite that exercise it.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class ConcurrentIngestionTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ConcurrentIngestionTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private static ITransactionCategorizationService BuildCategorizationService()
        {
            var service = Substitute.For<ITransactionCategorizationService>();
            service.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<TransactionCategory?>(), Arg.Any<CancellationToken>())
                .Returns(TransactionCategory.Uncategorized);
            return service;
        }

        [Fact]
        public async Task Handle_SameExternalTransactionIdSubmittedConcurrently_PersistsExactlyOnce()
        {
            using var customersSeedContext = _fixture.CreateCustomersContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Concurrency Test User");
            // Real flow (CompleteBankLinkCommandHandler -> AccountProvisioningAdapter): the
            // Account is created in the Customers module before the link is activated with
            // its real ID. A throwaway AccountId.Create() here (as several in-memory-provider
            // unit tests elsewhere use) would hide any FK against a real Postgres constraint
            // the in-memory provider doesn't enforce.
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Concurrency Test Account", AccountType.Checking, "ZAR");
            customersSeedContext.Customers.Add(customer);
            customersSeedContext.Accounts.Add(account);
            await customersSeedContext.SaveChangesAsync();

            using var bankLinksSeedContext = _fixture.CreateBankLinksContext();
            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(account.Id, "ext-acc-concurrency", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            bankLinksSeedContext.BankLinks.Add(link);
            await bankLinksSeedContext.SaveChangesAsync();

            var externalId = $"concurrent-txn-{Guid.NewGuid()}";
            var dto = new ExternalTransactionDTO
            {
                Id = externalId,
                Amount = -150m,
                Currency = "ZAR",
                Description = "Race condition test",
                Category = string.Empty,
                Date = DateTime.UtcNow
            };

            // Two independent DbContexts (as two concurrent requests would have),
            // both racing to insert the same external transaction ID. Each
            // TransactionsDbContext must be constructed with the SAME MessagingDbContext
            // instance the handler also receives — see the CreateContext doc comment.
            using var messagingA = _fixture.CreateMessagingContext();
            using var messagingB = _fixture.CreateMessagingContext();
            using var contextA = _fixture.CreateContext(messagingA);
            using var contextB = _fixture.CreateContext(messagingB);
            using var bankLinksA = _fixture.CreateBankLinksContext();
            using var bankLinksB = _fixture.CreateBankLinksContext();

            var handlerA = new ProcessInboundTransactionsCommandHandler(contextA, messagingA, new BankLinksReadApi(bankLinksA), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
            var handlerB = new ProcessInboundTransactionsCommandHandler(contextB, messagingB, new BankLinksReadApi(bankLinksB), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

            var command = new ProcessInboundTransactionsCommand("race-test-source", link.ExternalAccountId!, [dto]);

            var resultA = await handlerA.Handle(command, CancellationToken.None);
            var resultB = await handlerB.Handle(command, CancellationToken.None);

            resultA.IsSuccess.Should().BeTrue();
            resultB.IsSuccess.Should().BeTrue("a redelivered/racing duplicate must resolve as a no-op success, never a crash or a duplicate row");

            // Exactly one of the two actually inserted a row; the other's Value is 0.
            (resultA.Value + resultB.Value).Should().Be(1);

            using var verifyContext = _fixture.CreateContext();
            var count = await verifyContext.Transactions
                .CountAsync(t => t.CustomerId == customer.Id && t.Source.ExternalId == externalId);
            count.Should().Be(1, "the database-level unique constraint must guarantee exactly one row regardless of how many concurrent requests race to insert it");
        }

        /// <summary>
        /// Two genuinely concurrent batches that overlap on one external ID: whichever loses
        /// the race on the shared ID must still insert its own non-overlapping transaction
        /// (the handler discards the failed attempt and retries) instead of dropping its
        /// whole batch.
        /// </summary>
        [Fact]
        public async Task Handle_OverlappingBatchesRacing_PersistEveryTransactionExactlyOnce()
        {
            using var customersSeedContext = _fixture.CreateCustomersContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Overlap Test User");
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Overlap Test Account", AccountType.Checking, "ZAR");
            customersSeedContext.Customers.Add(customer);
            customersSeedContext.Accounts.Add(account);
            await customersSeedContext.SaveChangesAsync();

            using var bankLinksSeedContext = _fixture.CreateBankLinksContext();
            var externalAccountId = $"ext-acc-overlap-{Guid.NewGuid():N}";
            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(account.Id, externalAccountId, "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            bankLinksSeedContext.BankLinks.Add(link);
            await bankLinksSeedContext.SaveChangesAsync();

            ExternalTransactionDTO Dto(string id) => new()
            {
                Id = id,
                Amount = -10m,
                Currency = "ZAR",
                Description = "Overlap test",
                Category = string.Empty,
                Date = DateTime.UtcNow
            };

            var shared = $"shared-{Guid.NewGuid():N}";
            var onlyA = $"only-a-{Guid.NewGuid():N}";
            var onlyB = $"only-b-{Guid.NewGuid():N}";

            async Task<int> RunAsync(params string[] ids)
            {
                using var messaging = _fixture.CreateMessagingContext();
                using var context = _fixture.CreateContext(messaging);
                using var bankLinks = _fixture.CreateBankLinksContext();
                var handler = new ProcessInboundTransactionsCommandHandler(
                    context, messaging, new BankLinksReadApi(bankLinks), TestInstitutions.AllowAllDirectory(), TestNormalizers.Neutral, BuildCategorizationService(),
                    NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

                var result = await handler.Handle(
                    new ProcessInboundTransactionsCommand("overlap-test-source", externalAccountId, ids.Select(Dto).ToList()),
                    CancellationToken.None);

                result.IsSuccess.Should().BeTrue();
                return result.Value;
            }

            var inserted = await Task.WhenAll(RunAsync(shared, onlyA), RunAsync(shared, onlyB));

            inserted.Sum().Should().Be(3);

            using var verifyContext = _fixture.CreateContext();
            var stored = await verifyContext.Transactions
                .Where(t => t.CustomerId == customer.Id)
                .Select(t => t.Source.ExternalId)
                .ToListAsync();
            stored.Should().BeEquivalentTo([shared, onlyA, onlyB]);
        }
    }
}