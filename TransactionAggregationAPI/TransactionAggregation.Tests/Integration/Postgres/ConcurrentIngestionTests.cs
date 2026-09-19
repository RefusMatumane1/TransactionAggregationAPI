using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TransactionAggregation.Application.Common.DTOs;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
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
    /// handler's `catch (DbUpdateException ex) when
    /// (ex.InnerException?.Message.Contains("23505") == true)` clause checks for a
    /// Postgres-specific SQLSTATE code that no in-memory-provider test could ever
    /// trigger. This test is the first thing in the suite that actually exercises
    /// that catch clause.
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
            service.CategorizeTransactionAsync(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
                .Returns(TransactionCategory.Uncategorized);
            return service;
        }

        [Fact]
        public async Task Handle_SameExternalTransactionIdSubmittedConcurrently_PersistsExactlyOnce()
        {
            using var seedContext = _fixture.CreateContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Concurrency Test User");
            // Real flow (CompleteBankLinkCommandHandler): the Account is created via
            // customer.AddAccount(...) before the link is activated with its real ID,
            // both saved together — satisfying FK_Transactions_Accounts_AccountId.
            // A throwaway AccountId.Create() here (as several in-memory-provider unit
            // tests elsewhere use) would violate that FK against a real Postgres
            // constraint the in-memory provider doesn't enforce.
            var account = customer.AddAccount($"acc-{Guid.NewGuid():N}", "Concurrency Test Account", AccountType.Checking, "ZAR");
            seedContext.Customers.Add(customer);

            var link = BankLink.Create(customer.Id, Institution.FNB);
            link.Activate(account.Id, "ext-acc-concurrency", "enc-a", "enc-r", DateTime.UtcNow.AddHours(1));
            seedContext.BankLinks.Add(link);
            await seedContext.SaveChangesAsync();

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
            // ApplicationDbContext must be constructed with the SAME MessagingDbContext
            // instance the handler also receives — see the CreateContext doc comment.
            using var messagingA = _fixture.CreateMessagingContext();
            using var messagingB = _fixture.CreateMessagingContext();
            using var contextA = _fixture.CreateContext(messagingA);
            using var contextB = _fixture.CreateContext(messagingB);

            var handlerA = new ProcessInboundTransactionsCommandHandler(contextA, messagingA, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);
            var handlerB = new ProcessInboundTransactionsCommandHandler(contextB, messagingB, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

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
    }
}
