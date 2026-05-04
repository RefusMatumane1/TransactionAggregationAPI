using FluentAssertions;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Commands.CreateTransaction;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

public class CreateTransactionCommandHandlerTests
{
    private static readonly CategorizationOptions DefaultOptions = new()
    {
        Keywords = new Dictionary<string, string>
        {
            ["walmart"]    = "Groceries",
            ["grocery"]    = "Groceries",
            ["kroger"]     = "Groceries",
            ["restaurant"] = "Dining",
            ["starbucks"]  = "Dining",
            ["uber"]       = "Transportation",
            ["lyft"]       = "Transportation",
            ["netflix"]    = "Entertainment",
            ["spotify"]    = "Entertainment",
            ["electric"]   = "Utilities",
            ["rent"]       = "Housing",
            ["mortgage"]   = "Housing",
        }
    };

    private static TransactionCategorizationService BuildCategorizationService() =>
        new(Options.Create(DefaultOptions));

    private static CreateTransactionCommandHandler BuildHandler() =>
        new(InMemoryDbContextFactory.Create(), BuildCategorizationService());

    // ── Happy path ────────────────────────────────────────────────────────────

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
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());

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
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());
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
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());

        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, "ZAR",
            DateTime.UtcNow, "payment", "Manual");

        await handler.Handle(command, CancellationToken.None);

        context.Transactions.Single().AccountId.Should().BeNull();
    }

    // ── Auto-categorisation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("walmart weekly shop",    TransactionCategory.Groceries)]
    [InlineData("kroger checkout",        TransactionCategory.Groceries)]
    [InlineData("uber ride home",         TransactionCategory.Transportation)]
    [InlineData("lyft to airport",        TransactionCategory.Transportation)]
    [InlineData("netflix subscription",   TransactionCategory.Entertainment)]
    [InlineData("spotify premium",        TransactionCategory.Entertainment)]
    [InlineData("electric company",       TransactionCategory.Utilities)]
    [InlineData("rent payment",           TransactionCategory.Housing)]
    [InlineData("mortgage instalment",    TransactionCategory.Housing)]
    [InlineData("restaurant dinner",      TransactionCategory.Dining)]
    [InlineData("starbucks morning",      TransactionCategory.Dining)]
    public async Task Handle_AutoCategorisesFromDescription(string description, TransactionCategory expected)
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, "ZAR",
            DateTime.UtcNow, description, "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_PositiveAmount_CategorisedAsIncome()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), 5000m, "ZAR",
            DateTime.UtcNow, "salary payment", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Category.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task Handle_UnrecognisedNegativeDescription_CategorisedAsUncategorized()
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -75m, "ZAR",
            DateTime.UtcNow, "payment xyz", "Manual");

        await handler.Handle(command, CancellationToken.None);

        context.Transactions.Single().Category.Should().Be(TransactionCategory.Uncategorized);
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ZeroAmount_ReturnsValidationFailure()
    {
        var handler = BuildHandler();
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), 0m, "ZAR",
            DateTime.UtcNow, "bad amount", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]       // empty
    [InlineData("ZA")]     // too short
    [InlineData("ZARR")]   // too long
    public async Task Handle_InvalidCurrency_ReturnsValidationFailure(string currency)
    {
        var handler = BuildHandler();
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, currency,
            DateTime.UtcNow, "test", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── Multi-currency ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ZAR")]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    public async Task Handle_ValidCurrencyCodes_Succeed(string currency)
    {
        var context = InMemoryDbContextFactory.Create();
        var handler = new CreateTransactionCommandHandler(context, BuildCategorizationService());
        var command = new CreateTransactionCommand(
            Guid.NewGuid(), -100m, currency,
            DateTime.UtcNow, "test payment", "Manual");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Transactions.Single().Amount.Currency.Should().Be(currency.ToUpperInvariant());
    }
}
