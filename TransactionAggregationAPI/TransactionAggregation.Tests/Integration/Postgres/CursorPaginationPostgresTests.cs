using BuildingBlocks.Persistence.Pagination;
using FluentAssertions;
using Modules.Audit.Application.Features.SearchAuditEvents;
using Modules.Audit.Contracts;
using Modules.Audit.Domain;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class CursorPaginationPostgresTests(PostgresContainerFixture fixture)
    {
        private static readonly DateTime Day = new(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        private readonly RowValueKeysetPaginator _paginator = new();

        [Theory]
        [InlineData("date", true)]
        [InlineData("date", false)]
        [InlineData("amount", true)]
        [InlineData("amount", false)]
        public async Task Transactions_WalkingEveryPage_VisitsEachRowOnceInSortOrder_EvenWithTiedKeys(string sortBy, bool descending)
        {
            var bank = await SeedTransactionsAsync(count: 23);
            var seen = new List<TransactionListItemDtoView>();
            string? cursor = null;
            var pages = 0;

            do
            {
                using var context = fixture.CreateContext();
                var page = (await new GetTransactionsQueryHandler(context, new PostgresTransactionSearch(), _paginator).Handle(
                    new GetTransactionsQuery(TestFilters.For(bank)) { PageSize = 5, SortBy = sortBy, SortDescending = descending, Cursor = cursor },
                    CancellationToken.None)).Value;

                seen.AddRange(page.Items.Select(i => new TransactionListItemDtoView(i.Id, i.Date, i.Amount)));
                cursor = page.NextCursor;
                pages++;
            }
            while (cursor is not null);

            pages.Should().Be(5);
            seen.Select(s => s.Id).Should().HaveCount(23).And.OnlyHaveUniqueItems();
            var keys = seen.Select(s => KeyOf(s, sortBy)).ToList();
            if (descending)
                keys.Should().BeInDescendingOrder();
            else
                keys.Should().BeInAscendingOrder();
        }

        [Fact]
        public async Task AuditEvents_WalkingEveryPage_VisitsEachEventOnce_NewestFirst()
        {
            var source = $"cursor-{Guid.NewGuid():N}";
            using (var audit = fixture.CreateAuditContext())
            {
                for (var i = 0; i < 17; i++)
                    audit.AuditEvents.Add(AuditEvent.Create(Guid.NewGuid(), AuditEventTypes.InboundReceived,
                        Day.AddMinutes(i / 3), AuditChannels.Webhook, source, "ext-1", Guid.NewGuid()));
                await audit.SaveChangesAsync();
            }

            var seen = new List<(Guid Id, DateTime At)>();
            string? cursor = null;
            do
            {
                using var audit = fixture.CreateAuditContext();
                var page = (await new SearchAuditEventsQueryHandler(audit, _paginator).Handle(
                    new SearchAuditEventsQuery { SourceName = source, PageSize = 4, Cursor = cursor }, CancellationToken.None)).Value;
                seen.AddRange(page.Items.Select(e => (e.Id, e.OccurredAt)));
                cursor = page.NextCursor;
            }
            while (cursor is not null);

            seen.Select(s => s.Id).Should().HaveCount(17).And.OnlyHaveUniqueItems();
            seen.Select(s => s.At).Should().BeInDescendingOrder();
        }

        private static IComparable KeyOf(TransactionListItemDtoView row, string sortBy) => sortBy switch
        {
            "amount" => row.Amount,
            _ => row.Date
        };

        private async Task<string> SeedTransactionsAsync(int count)
        {
            var bank = $"cursor-{Guid.NewGuid():N}";
            using var context = fixture.CreateContext();
            for (var i = 0; i < count; i++)
            {
                context.Transactions.Add(TestTransactions.Create(
                    -(i % 4 + 1) * 10m, $"Row {i % 5}", (TransactionCategory)(i % 3 + 1),
                    institution: bank, account: "acc-1", date: Day.AddDays(-(i / 3))));
            }

            await context.SaveChangesAsync();
            return bank;
        }

        private sealed record TransactionListItemDtoView(Guid Id, DateTime Date, decimal Amount);
    }
}