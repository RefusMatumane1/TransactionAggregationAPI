using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Features.Transactions.Commands.ProcessInboundTransactions;
using Modules.Transactions.Domain.Enums;
using NSubstitute;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
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
            service.Categorize(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<TransactionCategory?>())
                .Returns(TransactionCategory.Uncategorized);
            return service;
        }

        // Runs an action once, at the moment the context is about to write: after the handler has
        // checked for existing rows, before it inserts. That is the check-then-insert race window.
        private sealed class RunInsideTheRaceWindow(Func<Task> action) : SaveChangesInterceptor
        {
            public bool Fired { get; private set; }

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (!Fired)
                {
                    Fired = true;
                    await action();
                }
                return result;
            }
        }

        [Fact]
        public async Task Handle_SameExternalTransactionIdSubmittedConcurrently_PersistsExactlyOnce()
        {
            var externalAccountId = $"ext-acc-concurrency-{Guid.NewGuid():N}";

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

            var command = new ProcessInboundTransactionsCommand("race-test-source", externalAccountId, null, [dto]);

            using var messagingB = _fixture.CreateMessagingContext();
            using var contextB = _fixture.CreateContext(messagingB);
            var handlerB = new ProcessInboundTransactionsCommandHandler(contextB, messagingB, TestNormalizers.Neutral, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

            // B runs to completion, and commits, after A has found no existing row but before A inserts.
            var resultB = default(SharedKernel.Common.Models.Result<int>);
            var raceWindow = new RunInsideTheRaceWindow(async () => resultB = await handlerB.Handle(command, CancellationToken.None));

            using var messagingA = _fixture.CreateMessagingContext();
            using var contextA = _fixture.CreateContext(messagingA, interceptors: raceWindow);
            var handlerA = new ProcessInboundTransactionsCommandHandler(contextA, messagingA, TestNormalizers.Neutral, BuildCategorizationService(), NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

            var resultA = await handlerA.Handle(command, CancellationToken.None);

            raceWindow.Fired.Should().BeTrue("the race must actually have been staged");
            resultB!.IsSuccess.Should().BeTrue();
            resultB.Value.Should().Be(1, "B saw no row and inserted first");
            resultA.IsSuccess.Should().BeTrue(
                "A's insert hits the unique index, and the handler must re-check and resolve it as a duplicate, never crash");
            resultA.Value.Should().Be(0, "A lost the race, so it must not have inserted anything");

            using var verifyContext = _fixture.CreateContext();
            var count = await verifyContext.Transactions
                .CountAsync(t => t.ExternalAccountId == externalAccountId && t.Source.ExternalId == externalId);
            count.Should().Be(1, "the database-level unique constraint must guarantee exactly one row regardless of how many concurrent requests race to insert it");
        }

        [Fact]
        public async Task Handle_OverlappingBatchesRacing_PersistEveryTransactionExactlyOnce()
        {
            var externalAccountId = $"ext-acc-overlap-{Guid.NewGuid():N}";

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
                var handler = new ProcessInboundTransactionsCommandHandler(
                context, messaging, TestNormalizers.Neutral, BuildCategorizationService(),
                NullLogger<ProcessInboundTransactionsCommandHandler>.Instance);

                var result = await handler.Handle(
                    new ProcessInboundTransactionsCommand("overlap-test-source", externalAccountId, null, ids.Select(Dto).ToList()),
                    CancellationToken.None);

                result.IsSuccess.Should().BeTrue();
                return result.Value;
            }

            var inserted = await Task.WhenAll(RunAsync(shared, onlyA), RunAsync(shared, onlyB));

            inserted.Sum().Should().Be(3);

            using var verifyContext = _fixture.CreateContext();
            var stored = await verifyContext.Transactions
                .Where(t => t.ExternalAccountId == externalAccountId)
                .Select(t => t.Source.ExternalId)
                .ToListAsync();
            stored.Should().BeEquivalentTo([shared, onlyA, onlyB]);
        }
    }
}