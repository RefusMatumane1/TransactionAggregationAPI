using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Services;

public class TransactionCategorizationServiceTests
{
    private static readonly CategorizationOptions DefaultOptions = new()
    {
        Keywords = new Dictionary<string, string>
        {
            ["walmart"] = "Groceries",
            ["kroger"] = "Groceries",
            ["grocery"] = "Groceries",
            ["restaurant"] = "Dining",
            ["starbucks"] = "Dining",
            ["uber"] = "Transportation",
            ["lyft"] = "Transportation",
            ["netflix"] = "Entertainment",
            ["spotify"] = "Entertainment",
            ["electric"] = "Utilities",
            ["water bill"] = "Utilities",
            ["rent"] = "Housing",
            ["mortgage"] = "Housing"
        }
    };

    private readonly TransactionCategorizationService _sut =
        new(Options.Create(DefaultOptions));

    private static Transaction MakeTransaction(decimal amount, string description)
    {
        return Transaction.Create(
            CustomerId.Create(),
            Money.Create(amount, "ZAR"),
            description,
            TransactionCategory.Uncategorized,
            TransactionSource.Create("test", Guid.NewGuid().ToString()));
    }

    [Theory]
    [InlineData("walmart grocery run", TransactionCategory.Groceries)]
    [InlineData("kroger checkout", TransactionCategory.Groceries)]
    [InlineData("restaurant bill", TransactionCategory.Dining)]
    [InlineData("starbucks morning coffee", TransactionCategory.Dining)]
    [InlineData("uber ride home", TransactionCategory.Transportation)]
    [InlineData("lyft to airport", TransactionCategory.Transportation)]
    [InlineData("netflix monthly subscription", TransactionCategory.Entertainment)]
    [InlineData("spotify premium", TransactionCategory.Entertainment)]
    [InlineData("electric company payment", TransactionCategory.Utilities)]
    [InlineData("water bill", TransactionCategory.Utilities)]
    [InlineData("rent payment", TransactionCategory.Housing)]
    [InlineData("mortgage instalment", TransactionCategory.Housing)]
    public async Task CategorizeAsync_KeywordMatch_ReturnsCorrectCategory(
        string description, TransactionCategory expected)
    {
        var tx = MakeTransaction(-100m, description);

        var result = await _sut.CategorizeTransactionAsync(tx, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task CategorizeAsync_PositiveAmount_ReturnsIncome()
    {
        var tx = MakeTransaction(1500m, "unknown source");

        var result = await _sut.CategorizeTransactionAsync(tx, CancellationToken.None);

        result.Should().Be(TransactionCategory.Income);
    }

    [Fact]
    public async Task CategorizeAsync_UnknownNegativeDescription_ReturnsUncategorized()
    {
        var tx = MakeTransaction(-50m, "payment xyz");

        var result = await _sut.CategorizeTransactionAsync(tx, CancellationToken.None);

        result.Should().Be(TransactionCategory.Uncategorized);
    }
}
