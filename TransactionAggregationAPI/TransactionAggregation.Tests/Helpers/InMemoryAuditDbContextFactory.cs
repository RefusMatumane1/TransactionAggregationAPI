using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Infrastructure.Persistence;
using NSubstitute;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryAuditDbContextFactory
{
    public static AuditDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new AuditDbContext(options, mediator);
    }
}