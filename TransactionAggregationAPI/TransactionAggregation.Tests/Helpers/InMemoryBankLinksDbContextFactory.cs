using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Modules.BankLinks.Infrastructure.Persistence;

namespace TransactionAggregation.Tests.Helpers;

public static class InMemoryBankLinksDbContextFactory
{
    public static BankLinksDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BankLinksDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Any<object>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

        return new BankLinksDbContext(options, mediator);
    }
}
