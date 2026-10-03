using FluentAssertions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class AggregationPostgresTests
    {
        private readonly PostgresContainerFixture _fixture;

        public AggregationPostgresTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Summary_AggregatesLedgerEntriesByMonthAndCategory_IgnoringPreLedgerRows()
        {
            var account = $"agg-{Guid.NewGuid():N}";
            await SeedAsync(account,
            [
                (-100.2500m, TransactionCategory.Groceries, TransactionStatus.Booked, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
                (-50.1250m, TransactionCategory.Groceries, TransactionStatus.Booked, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
                (2000m, TransactionCategory.Income, TransactionStatus.Booked, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
                (-30m, TransactionCategory.Dining, TransactionStatus.Booked, new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)),
                (-999m, TransactionCategory.Shopping, TransactionStatus.Pending, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
                (-5m, TransactionCategory.Dining, TransactionStatus.Expired, new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc))
            ]);

            using var context = _fixture.CreateContext();
            var handler = new GetTransactionSummaryQueryHandler(context);

            var result = await handler.Handle(
                new GetTransactionSummaryQuery(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TestFilters.For(TestInstitutions.FNB, account)), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var summary = result.Value;
            summary.TotalIncome.Should().Be(2000m);
            summary.TotalExpenses.Should().Be(180.3750m, "exact decimals, booked only");
            summary.NetBalance.Should().Be(1819.6250m);
            summary.SpendingByCategory.Should().BeEquivalentTo(new Dictionary<TransactionCategory, decimal>
            {
                [TransactionCategory.Groceries] = 150.3750m,
                [TransactionCategory.Dining] = 30m
            });
            summary.MonthlySummaries.Select(m => (m.Year, m.Month, m.TotalIncome, m.TotalExpenses, m.TransactionCount))
                .Should().Equal((2026, 1, 0m, 150.3750m, 2), (2026, 2, 2000m, 30m, 2));
            summary.TotalTransactions.Should().Be(4, "pending and expired rows from before the ledger are not transactions");
        }

        private async Task SeedAsync(
            string account,
            IReadOnlyList<(decimal Amount, TransactionCategory Category, TransactionStatus Status, DateTime Date)> rows)
        {
            using var context = _fixture.CreateContext();
            foreach (var (amount, category, status, date) in rows)
            {
                if (status == TransactionStatus.Booked)
                    context.Transactions.Add(TestTransactions.Create(amount, "Seeded", category, account: account, date: date));
                else
                    await PreLedgerRows.InsertAsync(context, TestInstitutions.FNB, account, Guid.NewGuid().ToString("N"), amount, status, date, category);
            }

            await context.SaveChangesAsync();
            await _fixture.RefreshDailyTotalsAsync();
        }
    }
}