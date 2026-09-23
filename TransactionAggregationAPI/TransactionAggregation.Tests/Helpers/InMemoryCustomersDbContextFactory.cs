using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Modules.Customers.Infrastructure.Persistence;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryCustomersDbContextFactory
{
    public static CustomersDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new CustomersDbContext(options, mediator);
    }
}
