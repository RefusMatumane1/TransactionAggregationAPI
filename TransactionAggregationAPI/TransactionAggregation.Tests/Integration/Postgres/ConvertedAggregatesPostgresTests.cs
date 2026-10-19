using FluentAssertions;
using Modules.Reporting.Application.DTOs;
using Modules.Reporting.Application.Features.Aggregates;
using Modules.Reporting.Application.Features.GetTransactionSummary;
using Modules.Transactions.Contracts;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class ConvertedAggregatesPostgresTests
    {
        private static readonly DateTime Day = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly ReportDay = DateOnly.FromDateTime(Day);

        private readonly PostgresContainerFixture _fixture;

        public ConvertedAggregatesPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

        private async Task<string> SeedAsync()
        {
            var account = $"fx-{Guid.NewGuid():N}";
            using var context = _fixture.CreateContext();
            context.Transactions.AddRange(
                TestTransactions.Create(1000m, "Salary", TransactionCategory.Income, account: account, date: Day, currency: "ZAR"),
                TestTransactions.Create(-300.5m, "Groceries", TransactionCategory.Groceries, account: account, date: Day.AddMinutes(1), currency: "ZAR"),
                TestTransactions.Create(-75.25m, "Online", TransactionCategory.Groceries, account: account, date: Day.AddMinutes(2), currency: "USD"),
                TestTransactions.Create(-10m, "Dinner", TransactionCategory.Dining, account: account, date: Day.AddMinutes(3), currency: "EUR"),
                TestTransactions.Create(-5000m, "Tokyo", TransactionCategory.Shopping, account: account, date: Day.AddMinutes(4), currency: "JPY"));
            await context.SaveChangesAsync();
            await _fixture.RefreshDailyTotalsAsync();
            return account;
        }

        [Fact]
        public async Task EveryConvertedView_TranslatesToSql_AndAddsUp()
        {
            var account = await SeedAsync();
            var filter = TestFilters.For(TestInstitutions.FNB, account);
            var catalog = TestCurrencies.Catalog;
            using var context = _fixture.CreateContext();

            using var reporting = _fixture.CreateReportingContext();
            var summary = (await new GetTransactionSummaryQueryHandler(reporting, catalog).Handle(
                new GetTransactionSummaryQuery(Day.AddDays(-1), Day.AddDays(1), filter, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var cashFlow = (await new GetCashFlowQueryHandler(reporting, catalog).Handle(
                new GetCashFlowQuery(ReportDay, ReportDay, filter, TimeGranularity.Day, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var institutions = (await new GetInstitutionBreakdownQueryHandler(reporting, catalog).Handle(
                new GetInstitutionBreakdownQuery(ReportDay, ReportDay, filter, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var comparison = (await new GetPeriodComparisonQueryHandler(reporting, catalog).Handle(
                new GetPeriodComparisonQuery(ReportDay, ReportDay, filter, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var categories = (await new GetCategoryBreakdownQueryHandler(reporting, catalog).Handle(
                new GetCategoryBreakdownQuery(ReportDay, ReportDay, filter, CashFlowDirection.Expense, ConvertTo: "ZAR"), CancellationToken.None)).Value;

            const decimal expenses = 300.5m + 75.25m * 18m + 10m * 20m;
            summary.TotalIncome.Should().Be(1000m);
            summary.TotalExpenses.Should().Be(expenses);
            cashFlow.TotalExpenses.Should().Be(expenses);
            cashFlow.Net.Should().Be(1000m - expenses);
            institutions.Institutions.Single().Expenses.Should().Be(expenses);
            comparison.Current.Expenses.Should().Be(expenses);
            categories.Total.Should().Be(expenses);
            cashFlow.Conversion!.UnconvertedCurrencies.Should().Equal("JPY");
            cashFlow.Conversion.Rates.Select(r => r.Currency).Should().Equal("EUR", "USD", "ZAR");
        }

        [Fact]
        public async Task ConvertedToAForeignCurrency_RoundsInTheDatabaseTheSameWayAsInMemory()
        {
            var account = await SeedAsync();
            using var context = _fixture.CreateContext();

            using var reporting = _fixture.CreateReportingContext();
            var summary = (await new GetTransactionSummaryQueryHandler(reporting, TestCurrencies.Catalog).Handle(
                new GetTransactionSummaryQuery(Day.AddDays(-1), Day.AddDays(1), TestFilters.For(TestInstitutions.FNB, account), ConvertTo: "USD"),
                CancellationToken.None)).Value;

            summary.TotalIncome.Should().Be(55.5556m);
            summary.SpendingByCategory[TransactionCategory.Dining].Should().Be(11.1111m);
            summary.SpendingByCategory[TransactionCategory.Groceries].Should().Be(16.6944m + 75.25m, "300.5 ZAR / 18 = 16.69444 -> 16.6944");
        }
    }
}