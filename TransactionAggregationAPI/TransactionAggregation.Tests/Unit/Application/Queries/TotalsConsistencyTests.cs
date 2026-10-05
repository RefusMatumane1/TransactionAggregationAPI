using FluentAssertions;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Features.Transactions.Queries.Aggregates;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class TotalsConsistencyTests
    {
        private static readonly DateTime Day = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateOnly ReportDay = DateOnly.FromDateTime(Day);

        private static async Task<TransactionsDbContext> SeedAsync()
        {
            Transaction Tx(decimal amount, int n, string institution = TestInstitutions.FNB, string currency = "ZAR") =>
                TestTransactions.Create(
                    amount, $"tx-{n}", amount > 0 ? TransactionCategory.Income : TransactionCategory.Groceries,
                    institution: institution, externalId: $"ext-{n}", date: Day.AddMinutes(n), currency: currency);

            var transactions = InMemoryDbContextFactory.Create();
            transactions.Transactions.AddRange(
                Tx(1000m, 1),
                Tx(-300m, 2),
                Tx(-200m, 3, TestInstitutions.Absa),
                Tx(-75m, 4, currency: "USD"));
            await transactions.SaveChangesAsync();
            await InMemoryDailyTotals.BuildAsync(transactions);

            return transactions;
        }

        private static Task<TransactionSummaryDto> SummaryAsync(TransactionsDbContext context, TransactionFilter filter, string currency = "ZAR") =>
            new GetTransactionSummaryQueryHandler(context)
                .Handle(new GetTransactionSummaryQuery(Day.AddDays(-1), Day.AddDays(1), filter, currency), CancellationToken.None)
                .ContinueWith(t => t.Result.Value);

        [Fact]
        public async Task Summary_TotalsOneCurrency_NeverMixingInAnother()
        {
            var context = await SeedAsync();

            var zar = await SummaryAsync(context, TestFilters.All);
            var usd = await SummaryAsync(context, TestFilters.All, "USD");

            zar.TotalIncome.Should().Be(1000m);
            zar.TotalExpenses.Should().Be(500m);
            zar.NetBalance.Should().Be(500m);
            zar.TotalTransactions.Should().Be(3);
            zar.SpendingByCategory[TransactionCategory.Groceries].Should().Be(500m);
            zar.Currency.Should().Be("ZAR");
            usd.TotalExpenses.Should().Be(75m);
            usd.TotalTransactions.Should().Be(1);
        }

        [Fact]
        public async Task EveryReadPath_AgreesOnTheNet()
        {
            var context = await SeedAsync();

            var summary = await SummaryAsync(context, TestFilters.All);
            var cashFlow = (await new GetCashFlowQueryHandler(context)
                .Handle(new GetCashFlowQuery(ReportDay, ReportDay, TestFilters.All, TimeGranularity.Day), CancellationToken.None)).Value;
            var institutions = (await new GetInstitutionBreakdownQueryHandler(context)
                .Handle(new GetInstitutionBreakdownQuery(ReportDay, ReportDay, TestFilters.All), CancellationToken.None)).Value;

            summary.NetBalance.Should().Be(500m);
            cashFlow.Net.Should().Be(summary.NetBalance);
            institutions.Institutions.Sum(i => i.Net).Should().Be(summary.NetBalance);
            institutions.Institutions.Select(i => (i.Institution, i.Net)).Should().BeEquivalentTo(
                [(TestInstitutions.FNB, 700m), (TestInstitutions.Absa, -200m)]);
        }

        [Fact]
        public async Task EveryReadPath_CountsOnlyTheInstitutionsTheCallerMayRead()
        {
            var context = await SeedAsync();
            var absaOnly = TestFilters.Only(TestInstitutions.Absa);

            var summary = await SummaryAsync(context, absaOnly);
            var cashFlow = (await new GetCashFlowQueryHandler(context)
                .Handle(new GetCashFlowQuery(ReportDay, ReportDay, absaOnly, TimeGranularity.Day), CancellationToken.None)).Value;
            var institutions = (await new GetInstitutionBreakdownQueryHandler(context)
                .Handle(new GetInstitutionBreakdownQuery(ReportDay, ReportDay, absaOnly), CancellationToken.None)).Value;

            summary.NetBalance.Should().Be(-200m);
            cashFlow.Net.Should().Be(-200m);
            institutions.Institutions.Should().ContainSingle().Which.Institution.Should().Be(TestInstitutions.Absa);
        }

        [Fact]
        public async Task ACallerWithNoInstitutions_SeesNothing()
        {
            var context = await SeedAsync();

            var summary = await SummaryAsync(context, TestFilters.Only());

            summary.TotalTransactions.Should().Be(0);
            summary.NetBalance.Should().Be(0m);
        }
    }
}