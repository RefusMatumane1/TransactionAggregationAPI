using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.Enums;
using SharedKernel.Common.ValueObjects;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Queries;

public class GetTransactionQueryHandlerTests
{
    private static Transaction MakeTransaction(decimal amount = -100m, string description = "test")
    {
        return Transaction.Create(
            CustomerId.Create(),
            Money.Create(amount, "ZAR"),
            description,
            TransactionCategory.Uncategorized,
            TransactionSource.Create("BogusBank", Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task Handle_ExistingTransaction_ReturnsMappedDto()
    {
        var context = InMemoryDbContextFactory.Create();
        var tx = MakeTransaction(-250m, "uber ride");
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var handler = new GetTransactionQueryHandler(
            context, NullLogger<GetTransactionQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetTransactionQuery(tx.Id.Value, tx.CustomerId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(tx.Id.Value);
        result.Value.Amount.Should().Be(-250m);
        result.Value.Description.Should().Be("uber ride");
        result.Value.SourceSystem.Should().Be("BogusBank");
    }

    [Fact]
    public async Task Handle_NonExistentTransaction_ReturnsNotFound()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new GetTransactionQueryHandler(
            context, NullLogger<GetTransactionQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetTransactionQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_AnotherCustomersTransaction_ReturnsTheSameNotFoundAsAMissingOne()
    {
        var context = InMemoryDbContextFactory.Create();
        var tx = MakeTransaction();
        context.Transactions.Add(tx);
        await context.SaveChangesAsync();

        var handler = new GetTransactionQueryHandler(
            context, NullLogger<GetTransactionQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetTransactionQuery(tx.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Modules.Transactions.Application.Common.Errors.TransactionErrors.NotFound(tx.Id.Value));
    }
}