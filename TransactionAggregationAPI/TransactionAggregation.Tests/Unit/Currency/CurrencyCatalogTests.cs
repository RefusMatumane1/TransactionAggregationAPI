using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Modules.Currency.Application;
using Modules.Currency.Contracts;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Currency
{
    public class CurrencyCatalogTests
    {
        private static ICurrencyCatalog Catalog => TestCurrencies.Catalog;

        [Fact]
        public void TheBase_IsAlwaysSupported_WithARateOfOne()
        {
            Catalog.BaseCurrency.Should().Be("ZAR");
            Catalog.IsSupported("ZAR").Should().BeTrue();
            Catalog.Rate("ZAR", "ZAR").Should().Be(1m);
            Catalog.Supported.Should().Equal("EUR", "GBP", "USD", "ZAR");
        }

        [Theory]
        [InlineData("USD", true)]
        [InlineData("usd", true)]
        [InlineData(" Usd ", true)]
        [InlineData("JPY", false)]
        [InlineData("XXX", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsSupported_MeansConfigured_NotJustIso(string? code, bool expected) =>
            Catalog.IsSupported(code).Should().Be(expected);

        [Fact]
        public void Convert_UsesTheRateToTheBase()
        {
            Catalog.Convert(10m, "USD", "ZAR").Should().Be(180m);
            Catalog.Convert(-2.5m, "EUR", "ZAR").Should().Be(-50m);
            Catalog.Convert(180m, "ZAR", "USD").Should().Be(10m);
        }

        [Fact]
        public void Convert_BetweenTwoForeignCurrencies_GoesThroughTheBase()
        {
            Catalog.Rate("EUR", "USD").Should().BeApproximately(20m / 18m, 0.0000000001m);
            Catalog.Convert(90m, "EUR", "USD").Should().Be(100m);
        }

        [Fact]
        public void Convert_RoundsToTheLedgersFourDecimals_HalvesAwayFromZero()
        {
            Catalog.Convert(100m, "ZAR", "USD").Should().Be(5.5556m);
            TestCurrencies.Create(("USD", 0.5m)).Convert(0.0001m, "USD", "ZAR").Should().Be(0.0001m, "0.00005 is a tie: away from zero");
            TestCurrencies.Create(("USD", 0.5m)).Convert(-0.0003m, "USD", "ZAR").Should().Be(-0.0002m, "-0.00015 is a tie: away from zero");
        }

        [Fact]
        public void Convert_ACurrencyWithoutARate_IsNull()
        {
            Catalog.Convert(10m, "JPY", "ZAR").Should().BeNull();
            Catalog.Convert(10m, "ZAR", "JPY").Should().BeNull();
            Catalog.Rate("JPY", "USD").Should().BeNull();
        }

        [Fact]
        public void Version_IdentifiesTheTable_AndChangesWithAnyRate()
        {
            var same = TestCurrencies.Create(("GBP", 24m), ("EUR", 20m), ("USD", 18m));
            var changed = TestCurrencies.Create(("USD", 18.01m), ("EUR", 20m), ("GBP", 24m));
            var added = TestCurrencies.Create(("USD", 18m), ("EUR", 20m), ("GBP", 24m), ("JPY", 0.12m));

            same.Version.Should().Be(Catalog.Version, "order does not matter");
            changed.Version.Should().NotBe(Catalog.Version);
            added.Version.Should().NotBe(Catalog.Version);
            Catalog.Version.Should().HaveLength(12);
        }

        [Fact]
        public void ARateOfZero_SwitchesACurrencyOff()
        {
            var catalog = TestCurrencies.Create(("USD", 18m), ("EUR", 0m));

            catalog.IsSupported("EUR").Should().BeFalse();
            catalog.Supported.Should().Equal("USD", "ZAR");
        }

        [Theory]
        [InlineData("zar", "USD", 18, "upper-case")]
        [InlineData("ZAR", "usd", 18, "upper-case")]
        [InlineData("ZA", "USD", 18, "3-letter")]
        [InlineData("ZAR", "U1D", 18, "3-letter")]
        [InlineData("ZAR", "USD", -1, "between 0")]
        [InlineData("ZAR", "USD", 2_000_000_000, "between 0")]
        public void InvalidConfiguration_IsRefusedWithTheReason(string baseCurrency, string code, double rate, string reason)
        {
            var options = new CurrencyOptions { BaseCurrency = baseCurrency, Rates = { [code] = (decimal)rate } };

            CurrencyOptions.Validate(options).Should().Contain(reason);
            CurrencyOptions.IsValid(options).Should().BeFalse();
            var act = () => new CurrencyCatalog(Options.Create(options));
            act.Should().Throw<InvalidOperationException>().WithMessage($"*{reason}*");
        }

        [Fact]
        public void TheBaseCannotBeGivenAnotherRate() =>
            CurrencyOptions.Validate(new CurrencyOptions { BaseCurrency = "ZAR", Rates = { ["ZAR"] = 2m } }).Should().Contain("base currency");

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        public void TheBaseCurrency_IsRequired_ThereIsNoBuiltInDefault(string baseCurrency) =>
            CurrencyOptions.Validate(new CurrencyOptions { BaseCurrency = baseCurrency, Rates = { ["USD"] = 18m } })
                .Should().Contain("BaseCurrency must be set");

        [Fact]
        public void ACurrencyIsAnyThreeLetterCode_TheConfigurationIsTheOnlyList()
        {
            var catalog = TestCurrencies.Create(("XAF", 0.03m), ("ABC", 2m));

            catalog.IsSupported("XAF").Should().BeTrue("there is no second, built-in list of real codes to agree with");
            catalog.IsSupported("ABC").Should().BeTrue();
            catalog.IsSupported("USD").Should().BeFalse("it is not configured here");
        }

        [Fact]
        public void AnotherBaseCurrency_ReversesTheRates()
        {
            var catalog = new CurrencyCatalog(Options.Create(new CurrencyOptions
            {
                BaseCurrency = "USD",
                Rates = { ["ZAR"] = 0.05m, ["EUR"] = 1.1m }
            }));

            catalog.Convert(100m, "EUR", "USD").Should().Be(110m);
            catalog.Convert(100m, "ZAR", "USD").Should().Be(5m);
        }

        [Fact]
        public void TheShippedConfiguration_IsValid_AndHasTheBase()
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "currencies.json"));
            var config = new ConfigurationBuilder()
                .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build();
            var options = config.GetSection(CurrencyOptions.SectionName).Get<CurrencyOptions>()!;

            CurrencyOptions.Validate(options).Should().BeNull();
            var catalog = new CurrencyCatalog(Options.Create(options));
            catalog.BaseCurrency.Should().Be("ZAR");
            catalog.IsSupported("USD").Should().BeTrue();
            catalog.Supported.Count.Should().BeGreaterThan(10);
        }
    }
}