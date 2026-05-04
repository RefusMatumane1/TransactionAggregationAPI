using FluentAssertions;
using TransactionAggregation.Application.Commands.CategorizeTransaction;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CategorizeTransactionCommandHandlerTests
{
    private static Transaction MakeTransaction(CustomerId customerId)
    {
        return Transaction.Create(
            customerId,
            Money.Create(-100m, "ZAR"),
            "test transaction",
            TransactionCategory.Uncategorized,
            TransactionSource.Create("TestSource", Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task Handle_ExistingTransaction_UpdatesCategory()
    {
        var context = InMemoryDbContextFactory.Create();
        var tx = MakeTransaction(CustomerId.Create());
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var handler = new CategorizeTransactionCommandHandler(context);
        var command = new CategorizeTransactionCommand(tx.Id.Value, TransactionCategory.Groceries);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(TransactionCategory.Groceries);
    }

    [Fact]
    public async Task Handle_NonExistentTransaction_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CategorizeTransactionCommandHandler(context);
        var command = new CategorizeTransactionCommand(Guid.NewGuid(), TransactionCategory.Dining);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData(TransactionCategory.Groceries)]
    [InlineData(TransactionCategory.Dining)]
    [InlineData(TransactionCategory.Transportation)]
    [InlineData(TransactionCategory.Entertainment)]
    [InlineData(TransactionCategory.Utilities)]
    [InlineData(TransactionCategory.Housing)]
    [InlineData(TransactionCategory.Income)]
    [InlineData(TransactionCategory.Shopping)]
    public async Task Handle_AcceptsAllValidCategories(TransactionCategory category)
    {
        var context = InMemoryDbContextFactory.Create();
        var tx = MakeTransaction(CustomerId.Create());
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var handler = new CategorizeTransactionCommandHandler(context);
        var result = await handler.Handle(
            new CategorizeTransactionCommand(tx.Id.Value, category),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(category);
    }
}
