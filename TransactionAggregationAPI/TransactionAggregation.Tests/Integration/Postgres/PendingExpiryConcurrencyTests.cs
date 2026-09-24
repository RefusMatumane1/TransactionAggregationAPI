using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Application.Features.Transactions.Commands.ExpireStalePendingTransactions;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// The expiry job and ingestion can both change the same pending row. The real race is
    /// "both read it as Pending, then both save" — the job's own WHERE Status = Pending
    /// can't catch that, only the xmin concurrency token can, and only against a real
    /// Postgres. These tests reproduce the interleaving directly: read on both sides, then save.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class PendingExpiryConcurrencyTests
    {
        private readonly PostgresContainerFixture _fixture;

        public PendingExpiryConcurrencyTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<TransactionId> SeedStalePendingAsync()
        {
            using var customers = _fixture.CreateCustomersContext();
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Expiry Race");
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Expiry", AccountType.Checking, "ZAR");
            customers.Customers.Add(customer);
            customers.Accounts.Add(account);
            await customers.SaveChangesAsync();

            using var context = _fixture.CreateContext();
            var tx = Transaction.Create(customer.Id, Money.Create(-80m, "ZAR"), "Hotel pre-auth", TransactionCategory.Uncategorized,
                TransactionSource.Create("FNB", $"auth-{Guid.NewGuid():N}"), account.Id, DateTime.UtcNow.AddDays(-30));
            context.Transactions.Add(tx);
            await context.SaveChangesAsync();
            tx.CreatedAt = DateTime.UtcNow.AddDays(-30);
            await context.SaveChangesAsync();
            return tx.Id;
        }

        [Fact]
        public async Task ExpiryThatReadTheRowBeforeASettlement_IsRejected_AndThePostingWins()
        {
            var id = await SeedStalePendingAsync();

            // The job reads the row as Pending and decides to expire it...
            using var job = _fixture.CreateContext();
            var jobView = await job.Transactions.SingleAsync(t => t.Id == id);
            jobView.Expire();

            // ...but before it saves, the bank's posting settles the row.
            using (var ingestion = _fixture.CreateContext())
            {
                var row = await ingestion.Transactions.SingleAsync(t => t.Id == id);
                row.Settle(Money.Create(-95m, "ZAR"));
                await ingestion.SaveChangesAsync();
            }

            var act = () => job.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "the job's read is stale — applying it would overwrite a real posting");

            using var verify = _fixture.CreateContext();
            var stored = await verify.Transactions.AsNoTracking().SingleAsync(t => t.Id == id);
            stored.Status.Should().Be(TransactionStatus.Settled);
            stored.Amount.Amount.Should().Be(-95m);
        }

        [Fact]
        public async Task SettlementThatReadTheRowBeforeAnExpiry_Fails_SoTheInboxRetriesAndThenSettles()
        {
            var id = await SeedStalePendingAsync();

            using var ingestion = _fixture.CreateContext();
            var ingestionView = await ingestion.Transactions.SingleAsync(t => t.Id == id);
            ingestionView.Settle(Money.Create(-95m, "ZAR"));

            using (var job = _fixture.CreateContext())
            {
                (await job.Transactions.SingleAsync(t => t.Id == id)).Expire();
                await job.SaveChangesAsync();
            }

            var act = () => ingestion.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();

            // The inbox retry re-reads the row (now Expired) — and a posting still settles it.
            using var retry = _fixture.CreateContext();
            var reread = await retry.Transactions.SingleAsync(t => t.Id == id);
            reread.Status.Should().Be(TransactionStatus.Expired);
            reread.Settle(Money.Create(-95m, "ZAR"));
            await retry.SaveChangesAsync();

            using var verify = _fixture.CreateContext();
            (await verify.Transactions.AsNoTracking().SingleAsync(t => t.Id == id)).Status.Should().Be(TransactionStatus.Settled);
        }

        [Fact]
        public async Task TwoReplicasThatBothReadTheRow_ExpireItExactlyOnce()
        {
            var id = await SeedStalePendingAsync();

            using var replicaA = _fixture.CreateContext();
            using var replicaB = _fixture.CreateContext();
            (await replicaA.Transactions.SingleAsync(t => t.Id == id)).Expire();
            (await replicaB.Transactions.SingleAsync(t => t.Id == id)).Expire();

            await replicaA.SaveChangesAsync();
            var act = () => replicaB.SaveChangesAsync();

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "the second replica's write is based on a stale read and must not apply (or audit) twice");
        }

        [Fact]
        public async Task Handler_ExpiresAStalePendingRow_AgainstRealPostgres()
        {
            var id = await SeedStalePendingAsync();

            // The candidate query is global, so settle any other stale pending rows earlier
            // tests left behind — this row must be the only one the handler can pick.
            using (var cleanup = _fixture.CreateContext())
            {
                foreach (var other in await cleanup.Transactions
                             .Where(t => t.Status == TransactionStatus.Pending && t.Id != id).ToListAsync())
                    other.Settle();
                await cleanup.SaveChangesAsync();
            }

            using var messaging = _fixture.CreateMessagingContext();
            using var context = _fixture.CreateContext(messaging);
            var handler = new ExpireStalePendingTransactionsCommandHandler(
                context, messaging, NullLogger<ExpireStalePendingTransactionsCommandHandler>.Instance);

            var result = await handler.Handle(
                new ExpireStalePendingTransactionsCommand(DateTime.UtcNow.AddDays(-7), 50), CancellationToken.None);

            result.Value.Should().Be(1);
            using var verify = _fixture.CreateContext();
            (await verify.Transactions.AsNoTracking().SingleAsync(t => t.Id == id)).Status.Should().Be(TransactionStatus.Expired);
        }
    }
}