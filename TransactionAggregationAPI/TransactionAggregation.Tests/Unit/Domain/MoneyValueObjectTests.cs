using FluentAssertions;
using Modules.Transactions.Domain.Common.ValueObjects;
using SharedKernel.Exceptions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Domain
{
    public class MoneyValueObjectTests
    {
        [Theory]
        [InlineData(100.00, "ZAR")]
        [InlineData(-250.50, "zar")]
        [InlineData(0.01, " ZAR ")]
        public void Create_WithValidArguments_Succeeds(decimal amount, string currency)
        {
            var money = Money.Create(amount, currency);

            money.Amount.Should().Be(amount);
            money.Currency.Should().Be("ZAR");
        }

        [Theory]
        [InlineData("USD")]
        [InlineData("EUR")]
        [InlineData("KWD")]
        [InlineData("jpy")]
        public void Create_WithAnyIso4217Currency_Succeeds(string currency)
        {
            Money.Create(100m, currency).Currency.Should().Be(currency.ToUpperInvariant());
        }

        [Theory]
        [InlineData("XAU")]
        [InlineData("ABC")]
        [InlineData("XTS")]
        public void Create_WithACodeThatIsNotAnAccountCurrency_ThrowsDomainException(string currency)
        {
            var act = () => Money.Create(100m, currency);

            act.Should().Throw<DomainException>().WithMessage("*ISO 4217*");
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
        [InlineData("ZA")]
        [InlineData("ZARR")]
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
        public void TwoMoneyObjects_WithSameAmountAndCurrency_AreEqual()
        {
            var a = Money.Create(100m, "ZAR");
            var b = Money.Create(100m, "ZAR");

            a.Should().Be(b);
        }

        [Fact]
        public void TwoMoneyObjects_WithDifferentAmounts_AreNotEqual()
        {
            var a = Money.Create(100m, "ZAR");
            var b = Money.Create(101m, "ZAR");

            a.Should().NotBe(b);
        }
    }
}