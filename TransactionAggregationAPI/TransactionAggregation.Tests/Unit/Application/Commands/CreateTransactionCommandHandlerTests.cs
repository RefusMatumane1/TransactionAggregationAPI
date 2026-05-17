using FluentAssertions;
using NSubstitute;
using TransactionAggregation.Application.Commands.CreateTransaction;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CreateTransactionCommandHandlerTests
{
    private static ITransactionValidator AlwaysValidValidator()
    {
        var v = Substitute.For<ITransactionValidator>();
        v.ValidateTransactionAsync(
                Arg.Any<Money>(),
                Arg.Any<string>(),
                Arg.Any<TransactionSource>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValidationResult { IsValid = true });
        return v;
    }

    private static CreateTransactionCommandHandler BuildHandler() =>
        new(InMemoryDbContextFactory.Create(), AlwaysValidValidator());

    [Fact]
    public async Task Handle_ValidCommand_StoresTransactionAndReturnsId()
    {
        var handler = BuildHandler();
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -150m, "ZAR",
            DateTime.UtcNow, "grocery store", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_TransactionStartsAsPending()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, AlwaysValidValidator());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, "ZAR",
            DateTime.UtcNow, "test payment", "Manual");

        await handler.Handle(command, CancellationToken.None);

        context.Transactions.Single().Status.Should().Be(TransactionStatus.Pending);
    }

    [Fact]
    public async Task Handle_WithAccountId_StoresAccountIdOnTransaction()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, AlwaysValidValidator());
        var accountId = Guid.NewGuid();

        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, "ZAR",
            DateTime.UtcNow, "payment", "Manual",
            AccountId: accountId);

        await handler.Handle(command, CancellationToken.None);

        context.Transactions.Single().AccountId!.Value.Should().Be(accountId);
    }

    [Fact]
    public async Task Handle_WithoutAccountId_AccountIdIsNull()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, AlwaysValidValidator());

        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, "ZAR",
            DateTime.UtcNow, "payment", "Manual");

        await handler.Handle(command, CancellationToken.None);

        context.Transactions.Single().AccountId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenValidatorReturnsFailure_ReturnsValidationError()
    {
        var validator = Substitute.For<ITransactionValidator>();
        validator.ValidateTransactionAsync(
                Arg.Any<Money>(),
                Arg.Any<string>(),
                Arg.Any<TransactionSource>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(ValidationResult.Failure("ZERO_AMOUNT", "Transaction amount cannot be zero"));

        var handler = new CreateTransactionCommandHandler(InMemoryDbContextFactory.Create(), validator);
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), 0m, "ZAR",
            DateTime.UtcNow, "bad amount", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZA")]
    [InlineData("ZARR")]
    public async Task Handle_InvalidCurrency_ReturnsValidationFailure(string currency)
    {
        var handler = BuildHandler();
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, currency,
            DateTime.UtcNow, "test", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("ZAR")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public async Task Handle_ValidCurrencyCodes_Succeed(string currency)
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, AlwaysValidValidator());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, currency,
            DateTime.UtcNow, "test payment", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Amount.Currency.Should().Be(currency.ToUpperInvariant());
    }
}
