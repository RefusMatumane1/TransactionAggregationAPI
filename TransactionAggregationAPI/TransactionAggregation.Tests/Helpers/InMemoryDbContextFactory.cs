using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TransactionAggregation.Persistence;

namespace TransactionAggregation.Tests.Helpers;

/// <summary>
/// Creates an isolated in-memory ApplicationDbContext per test.
/// Each call produces a uniquely named database so tests do not share state.
/// </summary>
public static class InMemoryDbContextFactory
{
    public static ApplicationDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        // Domain-event publishing is a side-effect we do not need in unit tests
        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new ApplicationDbContext(options, mediator);
    }
}
