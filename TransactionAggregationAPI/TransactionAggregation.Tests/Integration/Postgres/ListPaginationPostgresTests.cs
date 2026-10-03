using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Persistence.Pagination;
using FluentAssertions;
using Modules.WebhookSources.Application.Features.GetWebhookSources;
using Modules.WebhookSources.Domain;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // The real row-value paginator against PostgreSQL, walking a list end to end. Transaction
    // pages are covered by CursorPaginationPostgresTests.
    [Collection(PostgresCollection.Name)]
    public class ListPaginationPostgresTests(PostgresContainerFixture fixture)
    {
        private readonly RowValueKeysetPaginator _paginator = new();

        private static async Task<List<T>> WalkAsync<T>(Func<string?, Task<CursorPage<T>>> fetchPage, int expectedPages)
        {
            var seen = new List<T>();
            string? cursor = null;
            var pages = 0;
            do
            {
                var page = await fetchPage(cursor);
                seen.AddRange(page.Items);
                cursor = page.NextCursor;
                pages++;
            }
            while (cursor is not null);

            pages.Should().Be(expectedPages);
            return seen;
        }

        [Fact]
        public async Task WebhookSources_WalkingEveryPage_VisitsEachSourceOnce_ByName()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            await using (var seed = fixture.CreateWebhookSourcesContext(database))
            {
                foreach (var name in new[] { "source-e", "source-a", "source-d", "source-b", "source-c" })
                    seed.WebhookSources.Add(WebhookSource.Create(name, name, "#123456").Source);
                await seed.SaveChangesAsync();
            }

            var seen = await WalkAsync(async cursor =>
            {
                await using var context = fixture.CreateWebhookSourcesContext(database);
                return (await new GetWebhookSourcesQueryHandler(context, _paginator)
                    .Handle(new GetWebhookSourcesQuery(cursor, PageSize: 2), CancellationToken.None)).Value;
            }, expectedPages: 3);

            seen.Select(s => s.Code).Should().Equal("source-a", "source-b", "source-c", "source-d", "source-e");
        }
    }
}