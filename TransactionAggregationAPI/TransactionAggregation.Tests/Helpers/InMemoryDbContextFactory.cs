using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using BuildingBlocks.Messaging.Persistence;
using TransactionAggregation.Persistence;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryDbContextFactory
{
    private static IMediator BuildNoOpMediator()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
        return mediator;
    }

    /// <summary>
    /// ApplicationDbContext.SaveChangesAsync flushes the exact MessagingDbContext
    /// instance it was constructed with — pass one in (e.g. from
    /// InMemoryMessagingDbContextFactory.Create()) when a test needs to assert on
    /// Outbox messages an event handler wrote during this context's own save;
    /// omit it when the test never touches Outbox.
    /// </summary>
    public static ApplicationDbContext Create(string? dbName = null, MessagingDbContext? messagingDbContext = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, BuildNoOpMediator(), messagingDbContext ?? InMemoryMessagingDbContextFactory.Create());
    }
}

public static class InMemoryMessagingDbContextFactory
{
    public static MessagingDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new MessagingDbContext(options, mediator);
    }
}
