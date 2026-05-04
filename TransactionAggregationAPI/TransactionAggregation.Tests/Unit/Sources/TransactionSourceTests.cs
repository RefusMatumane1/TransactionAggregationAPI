using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Infrastructure.Providers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Sources;

public class BogusTransactionSourceTests
{
    private readonly BogusTransactionSource _sut =
        new(NullLogger<BogusTransactionSource>.Instance);

    [Fact]
    public async Task GetTransactionsAsync_ReturnsNonEmptyList()
    {
        var customerId = CustomerId.Create();
        var from = DateTime.UtcNow.AddMonths(-3);
        var to = DateTime.UtcNow;

        var result = await _sut.GetTransactionsAsync(customerId, from, to);

        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTransactionsAsync_AllTransactionsHaveRequiredFields()
    {
        var customerId = CustomerId.Create();
        var from = DateTime.UtcNow.AddMonths(-3);
        var to = DateTime.UtcNow;

        var result = await _sut.GetTransactionsAsync(customerId, from, to);

        foreach (var tx in result)
        {
            tx.Id.Should().NotBeNullOrEmpty();
            tx.Currency.Should().NotBeNullOrEmpty();
            tx.Description.Should().NotBeNullOrEmpty();
            tx.Amount.Should().NotBe(0);
        }
    }

    [Fact]
    public async Task GetTransactionsAsync_SourceNameIsCorrect()
    {
        _sut.SourceName.Should().Be("BogusBank");
    }
}

public class StaticDataTransactionSourceTests
{
    private readonly StaticDataTransactionSource _sut =
        new(NullLogger<StaticDataTransactionSource>.Instance);

    [Fact]
    public async Task GetTransactionsAsync_ReturnsKnownStaticData()
    {
        var customerId = CustomerId.Create();
        var from = DateTime.UtcNow.AddMonths(-3);
        var to = DateTime.UtcNow;

        var result = await _sut.GetTransactionsAsync(customerId, from, to);

        result.Should().NotBeEmpty();
        result.All(t => t.Currency == "ZAR").Should().BeTrue();
    }

    [Fact]
    public async Task GetTransactionsAsync_FiltersToDateRange()
    {
        var customerId = CustomerId.Create();
        var from = DateTime.UtcNow.AddDays(-2);
        var to = DateTime.UtcNow;

        var result = await _sut.GetTransactionsAsync(customerId, from, to);

        result.All(t => t.Date >= from && t.Date <= to).Should().BeTrue();
    }

    [Fact]
    public void SourceName_IsCorrect()
    {
        _sut.SourceName.Should().Be("DigitalWallet");
    }
}
