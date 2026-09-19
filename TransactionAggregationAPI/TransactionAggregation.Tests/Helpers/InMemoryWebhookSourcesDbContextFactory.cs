using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Modules.WebhookSources.Persistence;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryWebhookSourcesDbContextFactory
{
    public static WebhookSourcesDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<WebhookSourcesDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new WebhookSourcesDbContext(options, mediator);
    }
}
