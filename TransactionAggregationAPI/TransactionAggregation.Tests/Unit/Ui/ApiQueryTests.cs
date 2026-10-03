using FluentAssertions;
using System.Globalization;
using TransactionAggregationUI.Models.Transactions;
using TransactionAggregationUI.Services;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Ui
{
    public class ApiQueryTests
    {
        [Fact]
        public void Numbers_AreCultureInvariant_EvenWhereTheBrowserUsesADecimalComma()
        {
            var original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-ZA");

                ApiQuery.Of(("minAmount", 12.5m)).Should().Be("minAmount=12.5");
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void EnumsAreSentAsNumbers_AndEmptyValuesAreOmitted() =>
            ApiQuery.Of(("category", TransactionCategory.Groceries), ("searchTerm", ""), ("source", null), ("sortBy", "amount"))
                .Should().Be("category=1&sortBy=amount");

        [Fact]
        public void Values_AreUrlEscaped() =>
            ApiQuery.Of(("searchTerm", "fish & chips")).Should().Be("searchTerm=fish%20%26%20chips");
    }
}