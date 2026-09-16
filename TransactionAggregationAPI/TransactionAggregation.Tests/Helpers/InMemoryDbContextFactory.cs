using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TransactionAggregation.Persistence;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryDbContextFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new ApplicationDbContext(options, mediator);
    }
}