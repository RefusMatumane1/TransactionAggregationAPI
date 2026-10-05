using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Pagination;
using FluentAssertions;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class TransactionListTests
    {
        private static readonly DateTime Day = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

        private static async Task<TransactionsDbContext> SeedAsync()
        {
            var context = InMemoryDbContextFactory.Create();

            Transaction Tx(decimal amount, string description, TransactionCategory category, string source, int dayOffset, string currency = "ZAR") =>
                TestTransactions.Create(amount, description, category, institution: source, account: $"acc-{source}",
                    date: Day.AddDays(dayOffset), currency: currency);

            context.Transactions.AddRange(
                Tx(-50m, "Woolworths Food", TransactionCategory.Groceries, "FNB", 0),
                Tx(-500m, "Engen fuel", TransactionCategory.Transportation, "FNB", 1),
                Tx(2000m, "Salary ACME", TransactionCategory.Income, "Absa", 2),
                Tx(-20m, "Woolies coffee", TransactionCategory.Groceries, "Absa", 3, currency: "USD"));

            await context.SaveChangesAsync();
            return context;
        }

        private static GetTransactionsQueryHandler Handler(TransactionsDbContext context) =>
            new(context, new InMemoryTransactionSearch(), new InMemoryKeysetPaginator());

        private static async Task<IReadOnlyList<string>> ListAsync(
            Func<GetTransactionsQuery, GetTransactionsQuery> configure, TransactionFilter? filter = null)
        {
            var context = await SeedAsync();

            var result = await Handler(context).Handle(
                configure(new GetTransactionsQuery(filter ?? TestFilters.All) { PageSize = 100 }), CancellationToken.None);

            return result.Value.Items.Select(i => i.Description).ToList();
        }

        [Fact]
        public async Task Filter_ByCategory() =>
            (await ListAsync(q => q with { Category = TransactionCategory.Groceries }))
                .Should().BeEquivalentTo(["Woolworths Food", "Woolies coffee"]);

        [Fact]
        public async Task Filter_ByCurrency() =>
            (await ListAsync(q => q with { Currency = "USD" }))
                .Should().BeEquivalentTo(["Woolies coffee"]);

        [Fact]
        public async Task Filter_ByDateRange_IsInclusiveAtBothEnds() =>
            (await ListAsync(q => q with { FromDate = Day.AddDays(1), ToDate = Day.AddDays(2) }))
                .Should().BeEquivalentTo(["Engen fuel", "Salary ACME"]);

        [Fact]
        public async Task Filter_ByAmount_ComparesMagnitude_SoDebitsAndCreditsBothMatch() =>
            (await ListAsync(q => q with { MinAmount = 50m, MaxAmount = 500m }))
                .Should().BeEquivalentTo(["Woolworths Food", "Engen fuel"]);

        [Fact]
        public async Task Filter_BySearchTerm_IsCaseInsensitiveOverDescription() =>
            (await ListAsync(q => q with { SearchTerm = "WOOL" }))
                .Should().BeEquivalentTo(["Woolworths Food", "Woolies coffee"]);

        [Fact]
        public async Task Filter_ByInstitution() =>
            (await ListAsync(q => q, TestFilters.For("Absa")))
                .Should().BeEquivalentTo(["Salary ACME", "Woolies coffee"]);

        [Fact]
        public async Task Access_ScopedStaff_SeeOnlyTheirInstitutions() =>
            (await ListAsync(q => q, TestFilters.Only("FNB")))
                .Should().BeEquivalentTo(["Woolworths Food", "Engen fuel"]);

        [Fact]
        public async Task Access_AnInstitutionFilterOutsideTheCallersScope_ReturnsNothing() =>
            (await ListAsync(q => q, new TransactionFilter(InstitutionAccess.Only(["FNB"]), Institution: "Absa")))
                .Should().BeEmpty("narrowing can never widen what the caller may read");

        [Fact]
        public async Task Access_NoInstitutionsAssigned_ReturnsNothing() =>
            (await ListAsync(q => q, TestFilters.Only()))
                .Should().BeEmpty();

        [Fact]
        public async Task Sort_DefaultsToNewestFirst() =>
            (await ListAsync(q => q))
                .Should().ContainInOrder("Woolies coffee", "Salary ACME", "Engen fuel", "Woolworths Food");

        [Fact]
        public async Task Sort_ByAmountAscending() =>
            (await ListAsync(q => q with { SortBy = "amount", SortDescending = false }))
                .Should().ContainInOrder("Engen fuel", "Woolworths Food", "Woolies coffee", "Salary ACME");

        [Fact]
        public async Task Paging_FollowsTheCursor_AndReportsTheTotalOnlyWhenAsked()
        {
            var handler = Handler(await SeedAsync());

            var page1 = (await handler.Handle(
                new GetTransactionsQuery(TestFilters.All) { PageSize = 3, SortDescending = false, IncludeTotal = true },
                CancellationToken.None)).Value;
            var page2 = (await handler.Handle(
                new GetTransactionsQuery(TestFilters.All) { PageSize = 3, SortDescending = false, Cursor = page1.NextCursor },
                CancellationToken.None)).Value;

            page1.TotalCount.Should().Be(4);
            page1.TotalCountCapped.Should().BeFalse();
            page1.Items.Select(i => i.Description).Should().Equal("Woolworths Food", "Engen fuel", "Salary ACME");
            page2.Items.Select(i => i.Description).Should().Equal("Woolies coffee");
            page2.HasMore.Should().BeFalse();
            page2.TotalCount.Should().BeNull();
        }

        [Fact]
        public async Task BoundedCount_StopsAtTheLimit_AndSaysSo()
        {
            var rows = Enumerable.Range(0, BoundedCount.Limit + 5).AsQueryable();

            var count = await BoundedCount.OfAsync(rows, (q, _) => Task.FromResult(q.Count()), CancellationToken.None);

            count.Should().Be(new BoundedCount(BoundedCount.Limit, IsCapped: true));
        }

        [Theory]
        [InlineData("amount")]
        [InlineData("date")]
        public async Task Paging_EverySortKey_VisitsEachRowExactlyOnce(string sortBy)
        {
            var handler = Handler(await SeedAsync());

            var seen = new List<Guid>();
            string? cursor = null;
            do
            {
                var page = (await handler.Handle(
                    new GetTransactionsQuery(TestFilters.All) { PageSize = 1, SortBy = sortBy, Cursor = cursor },
                    CancellationToken.None)).Value;
                seen.AddRange(page.Items.Select(i => i.Id));
                cursor = page.NextCursor;
            }
            while (cursor is not null);

            seen.Should().HaveCount(4).And.OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData("category")]
        [InlineData("status")]
        [InlineData("description")]
        [InlineData("drop table")]
        public void Validator_RejectsSortsNoIndexServes(string sortBy) =>
            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(TestFilters.All) { SortBy = sortBy })
                .IsValid.Should().BeFalse();

        [Fact]
        public void Validator_RejectsACursorIssuedForADifferentSort()
        {
            var cursor = new PageCursor("amount", true, "10", Guid.NewGuid()).Encode();

            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(TestFilters.All) { SortBy = "date", Cursor = cursor })
                .IsValid.Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void Validator_RejectsPageSizesOutsideTheBound(int pageSize) =>
            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(TestFilters.All) { PageSize = pageSize })
                .IsValid.Should().BeFalse();

        [Theory]
        [InlineData("ab")]
        [InlineData("  a ")]
        public void Validator_RejectsSearchTermsTooShortForTheTrigramIndex(string term) =>
            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(TestFilters.All) { SearchTerm = term })
                .IsValid.Should().BeFalse();

        [Fact]
        public void Validator_RejectsAnAccountWithoutItsInstitution() =>
            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(new TransactionFilter(InstitutionAccess.All, ExternalAccountId: "acc-1")))
                .IsValid.Should().BeFalse("an account id is only unique within its bank");

        [Theory]
        [InlineData("zar")]
        [InlineData("XYZ")]
        public void Validator_RejectsCurrenciesThatAreNotUpperCaseIso4217(string currency) =>
            new GetTransactionsQueryValidator()
                .Validate(new GetTransactionsQuery(TestFilters.All) { Currency = currency })
                .IsValid.Should().BeFalse();
    }
}