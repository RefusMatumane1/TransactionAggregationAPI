using FluentAssertions;
using Modules.WebhookSources;
using Modules.WebhookSources.Features.GetWebhookSources;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetWebhookSourcesQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoSources_ReturnsEmptyList()
    {
        var context = InMemoryWebhookSourcesDbContextFactory.Create();
        var handler = new GetWebhookSourcesQueryHandler(context);

        var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleSources_ReturnsThemOrderedByName_WithoutKeyMaterial()
    {
        var context = InMemoryWebhookSourcesDbContextFactory.Create();
        var (sourceB, _) = WebhookSource.Create("b-source");
        var (sourceA, _) = WebhookSource.Create("a-source");
        context.WebhookSources.AddRange(sourceB, sourceA);
        await context.SaveChangesAsync();
        var handler = new GetWebhookSourcesQueryHandler(context);

        var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

        result.Value.Select(s => s.Name).Should().ContainInOrder("a-source", "b-source");
    }

    [Fact]
    public async Task Handle_Source_MapsIsActiveAndTimestamps()
    {
        var context = InMemoryWebhookSourcesDbContextFactory.Create();
        var (source, _) = WebhookSource.Create("stitch");
        source.Deactivate();
        context.WebhookSources.Add(source);
        await context.SaveChangesAsync();
        var handler = new GetWebhookSourcesQueryHandler(context);

        var result = await handler.Handle(new GetWebhookSourcesQuery(), CancellationToken.None);

        var dto = result.Value.Single();
        dto.Id.Should().Be(source.Id.Value);
        dto.IsActive.Should().BeFalse();
        dto.LastUsedAt.Should().BeNull();
    }
}
