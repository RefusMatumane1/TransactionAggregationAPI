using FluentAssertions;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Queries.Aggregates;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using SharedKernel.Common.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class ConvertedAggregatesTests
    {
        private static readonly DateTime Day = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly ReportDay = DateOnly.FromDateTime(Day);

        private static async Task<TransactionsDbContext> SeedAsync()
        {
            Transaction Tx(decimal amount, int n, string currency, TransactionCategory category) =>
                TestTransactions.Create(amount, $"tx-{n}", category, externalId: $"ext-{n}", date: Day.AddMinutes(n), currency: currency);

            var context = InMemoryDbContextFactory.Create();
            context.Transactions.AddRange(
                Tx(1000m, 1, "ZAR", TransactionCategory.Income),
                Tx(-300m, 2, "ZAR", TransactionCategory.Groceries),
                Tx(-75m, 3, "USD", TransactionCategory.Groceries),
                Tx(-10m, 4, "EUR", TransactionCategory.Dining),
                Tx(-5000m, 5, "JPY", TransactionCategory.Shopping));
            await context.SaveChangesAsync();
            await InMemoryDailyTotals.BuildAsync(context);
            return context;
        }

        private static GetTransactionSummaryQuery Summary(string? currency = null, string? convertTo = null) =>
            new(Day.AddDays(-1), Day.AddDays(1), TestFilters.All, currency, convertTo);

        private static async Task<TransactionSummaryDto> SummaryAsync(TransactionsDbContext context, GetTransactionSummaryQuery query) =>
            (await new GetTransactionSummaryQueryHandler(context, TestCurrencies.Catalog).Handle(query, CancellationToken.None)).Value;

        [Fact]
        public async Task Summary_ConvertsEveryConfiguredCurrencyIntoTheTarget()
        {
            var context = await SeedAsync();

            var summary = await SummaryAsync(context, Summary(convertTo: "ZAR"));

            summary.Currency.Should().Be("ZAR");
            summary.TotalIncome.Should().Be(1000m);
            summary.TotalExpenses.Should().Be(300m + 75m * 18m + 10m * 20m, "ZAR as is, USD at 18, EUR at 20");
            summary.NetBalance.Should().Be(1000m - 1850m);
            summary.TotalTransactions.Should().Be(4);
            summary.SpendingByCategory[TransactionCategory.Groceries].Should().Be(300m + 1350m);
            summary.SpendingByCategory[TransactionCategory.Dining].Should().Be(200m);
        }

        [Fact]
        public async Task Summary_ConvertedToAForeignCurrency_RoundsEachRowToTheLedgersDecimals()
        {
            var context = await SeedAsync();

            var summary = await SummaryAsync(context, Summary(convertTo: "USD"));

            summary.Currency.Should().Be("USD");
            summary.TotalIncome.Should().Be(55.5556m, "1000 ZAR / 18");
            summary.SpendingByCategory[TransactionCategory.Groceries].Should().Be(16.6667m + 75m, "300 ZAR / 18, plus 75 USD");
            summary.SpendingByCategory[TransactionCategory.Dining].Should().Be(11.1111m, "10 EUR * 20 / 18");
        }

        [Fact]
        public async Task Summary_ReportsTheRatesUsed_AndWhatItLeftOut()
        {
            var context = await SeedAsync();

            var conversion = (await SummaryAsync(context, Summary(convertTo: "ZAR"))).Conversion!;

            conversion.Target.Should().Be("ZAR");
            conversion.RatesVersion.Should().Be(TestCurrencies.Catalog.Version);
            conversion.Rates.Select(r => (r.Currency, r.Rate)).Should().Equal(("EUR", 20m), ("USD", 18m), ("ZAR", 1m));
            conversion.UnconvertedCurrencies.Should().Equal("JPY");
        }

        [Fact]
        public async Task WithoutConvertTo_NothingChanges_AndNoConversionIsReported()
        {
            var context = await SeedAsync();

            var zar = await SummaryAsync(context, Summary());
            var usd = await SummaryAsync(context, Summary("USD"));

            zar.TotalExpenses.Should().Be(300m);
            zar.Conversion.Should().BeNull();
            usd.TotalExpenses.Should().Be(75m);
            usd.Conversion.Should().BeNull();
        }

        [Fact]
        public async Task WithNoCurrency_EveryViewIsInTheConfiguredBase_NotABuiltInOne()
        {
            var context = await SeedAsync();
            var usdBase = TestCurrencies.Create("USD", ("ZAR", 0.05m), ("EUR", 1.1m));
            var handler = new GetTransactionSummaryQueryHandler(context, usdBase);

            var summary = (await handler.Handle(Summary(), CancellationToken.None)).Value;
            var zar = (await handler.Handle(Summary("ZAR"), CancellationToken.None)).Value;

            summary.Currency.Should().Be("USD");
            summary.TotalExpenses.Should().Be(75m, "only the USD entries, because USD is the base here");
            zar.Currency.Should().Be("ZAR", "an explicit currency still wins");
            zar.TotalExpenses.Should().Be(300m);
        }

        [Fact]
        public async Task ConvertTo_OverridesCurrency()
        {
            var context = await SeedAsync();

            var overridden = await SummaryAsync(context, Summary("USD", "ZAR"));
            var plain = await SummaryAsync(context, Summary(convertTo: "ZAR"));

            overridden.Should().BeEquivalentTo(plain);
        }

        [Theory]
        [InlineData("JPY")]
        [InlineData("XXX")]
        public async Task ConvertTo_AnUnconfiguredCurrency_IsAValidationError(string target)
        {
            var context = await SeedAsync();

            var result = await new GetTransactionSummaryQueryHandler(context, TestCurrencies.Catalog)
                .Handle(Summary(convertTo: target), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Type.Should().Be(ErrorType.Validation);
            result.Error.Description.Should().Contain("USD").And.Contain("ZAR");
        }

        [Fact]
        public async Task EveryConvertedReadPath_AgreesOnTheNet()
        {
            var context = await SeedAsync();
            var catalog = TestCurrencies.Catalog;
            const decimal expectedNet = 1000m - 1850m;

            var summary = await SummaryAsync(context, Summary(convertTo: "ZAR"));
            var cashFlow = (await new GetCashFlowQueryHandler(context, catalog)
                .Handle(new GetCashFlowQuery(ReportDay, ReportDay, TestFilters.All, TimeGranularity.Day, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var institutions = (await new GetInstitutionBreakdownQueryHandler(context, catalog)
                .Handle(new GetInstitutionBreakdownQuery(ReportDay, ReportDay, TestFilters.All, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var comparison = (await new GetPeriodComparisonQueryHandler(context, catalog)
                .Handle(new GetPeriodComparisonQuery(ReportDay, ReportDay, TestFilters.All, ConvertTo: "ZAR"), CancellationToken.None)).Value;
            var categories = (await new GetCategoryBreakdownQueryHandler(context, catalog)
                .Handle(new GetCategoryBreakdownQuery(ReportDay, ReportDay, TestFilters.All, CashFlowDirection.Expense, ConvertTo: "ZAR"), CancellationToken.None)).Value;

            summary.NetBalance.Should().Be(expectedNet);
            cashFlow.Net.Should().Be(expectedNet);
            institutions.Institutions.Sum(i => i.Net).Should().Be(expectedNet);
            comparison.Current.Net.Should().Be(expectedNet);
            categories.Total.Should().Be(1850m);
            foreach (var conversion in new[] { cashFlow.Conversion, institutions.Conversion, comparison.Conversion, categories.Conversion })
            {
                conversion.Should().NotBeNull();
                conversion!.UnconvertedCurrencies.Should().Equal("JPY");
            }
        }

        [Fact]
        public async Task ConvertedViews_StillHonourTheCallersInstitutions()
        {
            var context = await SeedAsync();

            var summary = await SummaryAsync(context, Summary(convertTo: "ZAR") with { Filter = TestFilters.Only("NoSuchBank") });

            summary.TotalIncome.Should().Be(0m);
            summary.TotalExpenses.Should().Be(0m);
            summary.Conversion!.Rates.Should().BeEmpty();
        }

        [Fact]
        public async Task TheTransactionList_AddsEachEntrysConvertedAmount()
        {
            var context = await SeedAsync();
            var handler = new GetTransactionsQueryHandler(context, new InMemoryTransactionSearch(), new InMemoryKeysetPaginator(), TestCurrencies.Catalog);

            var page = (await handler.Handle(new GetTransactionsQuery(TestFilters.All) { ConvertTo = "ZAR", PageSize = 50 }, CancellationToken.None)).Value;

            page.Items.Single(i => i.Currency == "USD").ConvertedAmount.Should().Be(-1350m);
            page.Items.Single(i => i.Currency == "EUR").ConvertedAmount.Should().Be(-200m);
            page.Items.Single(i => i.Currency == "ZAR" && i.Amount == 1000m).ConvertedAmount.Should().Be(1000m);
            page.Items.Single(i => i.Currency == "JPY").ConvertedAmount.Should().BeNull("JPY has no rate");
            page.Items.Should().OnlyContain(i => i.ConvertedCurrency == "ZAR");
        }

        [Fact]
        public async Task TheTransactionList_WithoutConvertTo_IsAsRecorded()
        {
            var context = await SeedAsync();
            var handler = new GetTransactionsQueryHandler(context, new InMemoryTransactionSearch(), new InMemoryKeysetPaginator(), TestCurrencies.Catalog);

            var page = (await handler.Handle(new GetTransactionsQuery(TestFilters.All) { PageSize = 50 }, CancellationToken.None)).Value;

            page.Items.Should().OnlyContain(i => i.ConvertedAmount == null && i.ConvertedCurrency == null);
        }

        [Fact]
        public async Task TheTransactionList_UnsupportedConvertTo_IsAValidationError()
        {
            var context = await SeedAsync();
            var handler = new GetTransactionsQueryHandler(context, new InMemoryTransactionSearch(), new InMemoryKeysetPaginator(), TestCurrencies.Catalog);

            var result = await handler.Handle(new GetTransactionsQuery(TestFilters.All) { ConvertTo = "JPY" }, CancellationToken.None);

            result.Error.Type.Should().Be(ErrorType.Validation);
        }

        [Fact]
        public async Task TheTransactionDetail_CarriesTheConvertedAmount()
        {
            var context = await SeedAsync();
            var usd = context.Transactions.AsEnumerable().Single(t => t.Amount.Currency == "USD");
            var handler = new GetTransactionQueryHandler(context, TestCurrencies.Catalog);

            var converted = (await handler.Handle(
                new GetTransactionQuery(usd.Id.Value, BuildingBlocks.Application.Abstractions.Authentication.InstitutionAccess.All) { ConvertTo = "ZAR" },
                CancellationToken.None)).Value;
            var plain = (await handler.Handle(
                new GetTransactionQuery(usd.Id.Value, BuildingBlocks.Application.Abstractions.Authentication.InstitutionAccess.All),
                CancellationToken.None)).Value;

            converted.ConvertedAmount.Should().Be(-1350m);
            converted.ConvertedCurrency.Should().Be("ZAR");
            plain.ConvertedAmount.Should().BeNull();
        }
    }
}