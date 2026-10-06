using FluentAssertions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Queries.Aggregates;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // Shared database: each test uses its own bank codes.
    [Collection(PostgresCollection.Name)]
    public class TransactionAggregatesPostgresTests(PostgresContainerFixture fixture)
    {
        private static DateTime Utc(int year, int month, int day, int hour, int minute = 0) =>
            new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        private static string NewBank() => $"agg-{Guid.NewGuid():N}";

        [Fact]
        public async Task CashFlow_BucketsBySouthAfricanDay_ZeroFillsGaps_AndIgnoresPreLedgerRows()
        {
            var bank = NewBank();
            await SeedAsync(bank, "acc-1",
            [
                (-100m, TransactionCategory.Groceries, Utc(2026, 1, 31, 21, 30), true),
                (-40m, TransactionCategory.Dining, Utc(2026, 1, 31, 22, 30), true),
                (500m, TransactionCategory.Income, Utc(2026, 2, 2, 10), true),
                (-999m, TransactionCategory.Shopping, Utc(2026, 2, 1, 10), false)
            ]);
            var filter = TestFilters.For(bank);

            using var context = fixture.CreateContext();
            var daily = (await new GetCashFlowQueryHandler(context).Handle(
                new GetCashFlowQuery(new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 3), filter, TimeGranularity.Day),
                CancellationToken.None)).Value;
            var monthly = (await new GetCashFlowQueryHandler(context).Handle(
                new GetCashFlowQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), filter, TimeGranularity.Month),
                CancellationToken.None)).Value;

            daily.Points.Select(p => (p.PeriodStart, p.Income, p.Expenses, p.TransactionCount)).Should().Equal(
                (new DateOnly(2026, 1, 31), 0m, 100m, 1),
                (new DateOnly(2026, 2, 1), 0m, 40m, 1),
                (new DateOnly(2026, 2, 2), 500m, 0m, 1),
                (new DateOnly(2026, 2, 3), 0m, 0m, 0));
            monthly.Points.Select(p => (p.PeriodStart.Month, p.Expenses)).Should().Equal((1, 100m), (2, 40m));
            monthly.Net.Should().Be(360m);
            monthly.Currency.Should().Be("ZAR");
        }

        [Fact]
        public async Task CategoryBreakdown_SharesSumToOne_AndTheDirectionSelectsDebitsOrCredits()
        {
            var bank = NewBank();
            await SeedAsync(bank, "acc-1",
            [
                (-75m, TransactionCategory.Groceries, Utc(2026, 3, 2, 9), true),
                (-25m, TransactionCategory.Dining, Utc(2026, 3, 3, 9), true),
                (1000m, TransactionCategory.Income, Utc(2026, 3, 4, 9), true)
            ]);
            var filter = TestFilters.For(bank);

            using var context = fixture.CreateContext();
            var handler = new GetCategoryBreakdownQueryHandler(context);
            var spend = (await handler.Handle(
                new GetCategoryBreakdownQuery(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), filter), CancellationToken.None)).Value;
            var income = (await handler.Handle(
                new GetCategoryBreakdownQuery(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), filter, CashFlowDirection.Income),
                CancellationToken.None)).Value;

            spend.Total.Should().Be(100m);
            spend.Categories.Select(c => (c.Category, c.Amount, c.Share)).Should().Equal(
                (TransactionCategory.Groceries, 75m, 0.75m), (TransactionCategory.Dining, 25m, 0.25m));
            income.Categories.Should().ContainSingle().Which.Amount.Should().Be(1000m);
        }

        [Fact]
        public async Task Institutions_GroupByBank_TotalLedgerMoney_AndCountAccounts()
        {
            var prefix = NewBank();
            var fnb = $"{prefix}-FNB";
            var capitec = $"{prefix}-Capitec";
            await SeedAsync(fnb, "cheque", [(-60m, TransactionCategory.Groceries, Utc(2026, 4, 5, 9), true)]);
            await SeedAsync(fnb, "savings", [(200m, TransactionCategory.Transfer, Utc(2026, 4, 6, 9), true)]);
            await SeedAsync(capitec, "card",
            [
                (-10m, TransactionCategory.Dining, Utc(2026, 4, 7, 9), true),
                (-5m, TransactionCategory.Dining, Utc(2026, 4, 8, 9), false)
            ]);

            using var context = fixture.CreateContext();
            var result = (await new GetInstitutionBreakdownQueryHandler(context).Handle(
                new GetInstitutionBreakdownQuery(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30), TestFilters.All),
                CancellationToken.None)).Value;

            var mine = result.Institutions.Where(i => i.Institution.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            mine.Select(i => i.Institution).Should().Equal(fnb, capitec);
            mine[0].Net.Should().Be(140m);
            mine[0].AccountCount.Should().Be(2);
            mine[0].Accounts.Select(a => a.ExternalAccountId).Should().BeEquivalentTo(["cheque", "savings"]);
            mine[1].Expenses.Should().Be(10m, "a pre-ledger pending row is not a transaction");
            mine[1].AccountCount.Should().Be(1);
        }

        [Fact]
        public async Task Comparison_SetsThePeriodAgainstTheEquallyLongPeriodBefore()
        {
            var bank = NewBank();
            await SeedAsync(bank, "acc-1",
            [
                (-100m, TransactionCategory.Groceries, Utc(2026, 5, 10, 9), true),
                (-80m, TransactionCategory.Groceries, Utc(2026, 4, 10, 9), true),
                (-30m, TransactionCategory.Dining, Utc(2026, 4, 12, 9), true)
            ]);

            using var context = fixture.CreateContext();
            var result = (await new GetPeriodComparisonQueryHandler(context).Handle(
                new GetPeriodComparisonQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), TestFilters.For(bank)),
                CancellationToken.None)).Value;

            result.Previous.Period.Should().Be(new PeriodDto(new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)));
            result.Current.Expenses.Should().Be(100m);
            result.Previous.Expenses.Should().Be(110m);
            result.ExpensesChangePercent.Should().Be(-9.09m);
            result.Categories.Select(c => (c.Category, c.Change)).Should().Equal(
                (TransactionCategory.Dining, -30m), (TransactionCategory.Groceries, 20m));
        }

        [Fact]
        public async Task Summary_UsesSouthAfricanMonths()
        {
            var bank = NewBank();
            await SeedAsync(bank, "acc-1", [(-40m, TransactionCategory.Dining, Utc(2026, 6, 30, 22, 30), true)]);

            using var context = fixture.CreateContext();
            var summary = (await new GetTransactionSummaryQueryHandler(context).Handle(
                new GetTransactionSummaryQuery(Utc(2026, 6, 1, 0), Utc(2026, 7, 31, 0), TestFilters.For(bank)),
                CancellationToken.None)).Value;

            summary.MonthlySummaries.Should().ContainSingle().Which.Month.Should().Be(7, "22:30 UTC on 30 June is 00:30 on 1 July in South Africa");
        }

        private async Task SeedAsync(
            string bank,
            string account,
            IReadOnlyList<(decimal Amount, TransactionCategory Category, DateTime Date, bool Booked)> rows)
        {
            using var context = fixture.CreateContext();
            foreach (var (amount, category, date, booked) in rows)
            {
                if (booked)
                    context.Transactions.Add(TestTransactions.Create(
                        amount, "Aggregate test", category, institution: bank, account: account, date: date));
                else
                    await PreLedgerRows.InsertAsync(
                        context, bank, account, Guid.NewGuid().ToString("N"), amount, TransactionStatus.Pending, date, category);
            }

            await context.SaveChangesAsync();
            await fixture.RefreshDailyTotalsAsync();
        }

        [Fact]
        public async Task Aggregates_AreComputedPerCurrency_NeverMixingCurrencies()
        {
            var bank = NewBank();
            await SeedAsync(bank, "acc-1", [(-100m, TransactionCategory.Groceries, Utc(2026, 8, 2, 9), true)]);
            using (var seed = fixture.CreateContext())
            {
                seed.Transactions.Add(TestTransactions.Create(-7m, "Abroad", TransactionCategory.Dining,
                    institution: bank, account: "acc-1", date: Utc(2026, 8, 3, 9), currency: "EUR"));
                await seed.SaveChangesAsync();
            }
            await fixture.RefreshDailyTotalsAsync();

            using var context = fixture.CreateContext();
            var handler = new GetCategoryBreakdownQueryHandler(context);
            var zar = (await handler.Handle(
                new GetCategoryBreakdownQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), TestFilters.For(bank)),
                CancellationToken.None)).Value;
            var eur = (await handler.Handle(
                new GetCategoryBreakdownQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), TestFilters.For(bank), Currency: "EUR"),
                CancellationToken.None)).Value;

            zar.Total.Should().Be(100m);
            zar.Currency.Should().Be("ZAR");
            eur.Total.Should().Be(7m);
            eur.Currency.Should().Be("EUR");
        }
    }
}