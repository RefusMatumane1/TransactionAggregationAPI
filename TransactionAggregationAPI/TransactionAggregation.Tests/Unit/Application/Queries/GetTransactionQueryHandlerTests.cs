using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Application.Common.Enums;
using TransactionAggregation.Application.Queries.Transaction.GetTransaction;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
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
            new GetTransactionQuery(tx.Id.Value), CancellationToken.None);

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
            new GetTransactionQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }
}
