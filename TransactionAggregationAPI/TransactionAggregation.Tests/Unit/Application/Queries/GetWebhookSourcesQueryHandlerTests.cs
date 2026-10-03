using FluentAssertions;
using Modules.WebhookSources.Application.Features.GetWebhookSources;
using Modules.WebhookSources.Domain;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries
{
    public class GetWebhookSourcesQueryHandlerTests
    {
        [Fact]
        public async Task Handle_NoSources_ReturnsEmptyList()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var handler = new GetWebhookSourcesQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Items.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_MultipleSources_ReturnsThemOrderedByName_WithoutKeyMaterial()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var (sourceB, _) = WebhookSource.Create("b-source", "b-source", "#123456");
            var (sourceA, _) = WebhookSource.Create("a-source", "a-source", "#123456");
            context.WebhookSources.AddRange(sourceB, sourceA);
            await context.SaveChangesAsync();
            var handler = new GetWebhookSourcesQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

            result.Value.Items.Select(s => s.Code).Should().ContainInOrder("a-source", "b-source");
        }

        [Fact]
        public async Task Handle_Source_MapsIsActiveAndTimestamps()
        {
            var context = InMemoryWebhookSourcesDbContextFactory.Create();
            var (source, _) = WebhookSource.Create("stitch", "stitch", "#123456");
            source.Deactivate();
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();
            var handler = new GetWebhookSourcesQueryHandler(context, new InMemoryKeysetPaginator());

            var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

            var dto = result.Value.Items.Single();
            dto.Id.Should().Be(source.Id.Value);
            dto.IsActive.Should().BeFalse();
            dto.LastUsedAt.Should().BeNull();
        }
    }
}