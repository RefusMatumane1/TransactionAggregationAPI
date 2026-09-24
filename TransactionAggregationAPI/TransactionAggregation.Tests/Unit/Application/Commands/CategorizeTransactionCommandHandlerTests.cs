using FluentAssertions;
using Modules.Transactions.Application.Features.Transactions.Commands.CategorizeTransaction;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
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
        var command = new CategorizeTransactionCommand(tx.Id.Value, tx.CustomerId.Value, TransactionCategory.Groceries);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(TransactionCategory.Groceries);
    }

    [Fact]
    public async Task Handle_NonExistentTransaction_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CategorizeTransactionCommandHandler(context);
        var command = new CategorizeTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), TransactionCategory.Dining);

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
            new CategorizeTransactionCommand(tx.Id.Value, tx.CustomerId.Value, category),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(category);
    }

    [Fact]
    public async Task Handle_AnotherCustomersTransaction_ReturnsNotFoundAndLeavesItUnchanged()
    {
        var context = InMemoryDbContextFactory.Create();
        var tx = MakeTransaction(CustomerId.Create());
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var handler = new CategorizeTransactionCommandHandler(context);
        var result = await handler.Handle(
            new CategorizeTransactionCommand(tx.Id.Value, Guid.NewGuid(), TransactionCategory.Groceries),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Modules.Transactions.Application.Common.Errors.TransactionErrors.NotFound(tx.Id.Value));
        context.Transactions.Single().Category.Should().Be(TransactionCategory.Uncategorized);
    }
}