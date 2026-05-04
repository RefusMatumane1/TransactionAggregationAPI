using FluentAssertions;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain;

public class MoneyValueObjectTests
{
    // ── Creation ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(100.00,  "ZAR")]
    [InlineData(-250.50, "USD")]
    [InlineData(0.01,    "EUR")]
    public void Create_WithValidArguments_Succeeds(decimal amount, string currency)
    {
        var money = Money.Create(amount, currency);

        money.Amount.Should().Be(amount);
        money.Currency.Should().Be(currency.ToUpperInvariant());
    }

    [Fact]
    public void Create_WithZeroAmount_ThrowsDomainException()
    {
        var act = () => Money.Create(0, "ZAR");

        act.Should().Throw<DomainException>()
           .WithMessage("*zero*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ZA")]       // too short
    [InlineData("ZARR")]     // too long
    public void Create_WithInvalidCurrencyCode_ThrowsDomainException(string currency)
    {
        var act = () => Money.Create(100m, currency);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_CurrencyIsNormalisedToUpperCase()
    {
        var money = Money.Create(100m, "zar");

        money.Currency.Should().Be("ZAR");
    }

    // ── Helper properties ─────────────────────────────────────────────────────

    [Fact]
    public void IsIncome_ForPositiveAmount_IsTrue()
    {
        var money = Money.Create(500m, "ZAR");

        money.IsIncome.Should().BeTrue();
        money.IsExpense.Should().BeFalse();
    }

    [Fact]
    public void IsExpense_ForNegativeAmount_IsTrue()
    {
        var money = Money.Create(-200m, "ZAR");

        money.IsExpense.Should().BeTrue();
        money.IsIncome.Should().BeFalse();
    }

    [Fact]
    public void AbsoluteAmount_IsAlwaysPositive()
    {
        Money.Create(-75.50m, "ZAR").AbsoluteAmount.Should().Be(75.50m);
        Money.Create(75.50m,  "ZAR").AbsoluteAmount.Should().Be(75.50m);
    }

    // ── Equality ──────────────────────────────────────────────────────────────

    [Fact]
    public void TwoMoneyObjects_WithSameAmountAndCurrency_AreEqual()
    {
        var a = Money.Create(100m, "ZAR");
        var b = Money.Create(100m, "ZAR");

        a.Should().Be(b);
    }

    [Fact]
    public void TwoMoneyObjects_WithDifferentCurrencies_AreNotEqual()
    {
        var a = Money.Create(100m, "ZAR");
        var b = Money.Create(100m, "USD");

        a.Should().NotBe(b);
    }
}
