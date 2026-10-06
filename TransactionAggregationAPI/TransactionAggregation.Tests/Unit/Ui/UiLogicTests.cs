// The UI is referenced under the "ui" alias: it has its own top-level Program, like the API.
extern alias ui;

using FluentAssertions;
using System.Net;
using System.Text;
using ui::TransactionAggregationUI.Models.Aggregates;
using ui::TransactionAggregationUI.Services;
using ui::TransactionAggregationUI.Shared;
using Xunit;

namespace TransactionAggregation.Tests.Unit.UI
{
    // The UI's pure logic: how money, dates and changes are shown, the period presets, the query
    // strings sent to the API, and how API failures become messages a user can act on.
    public class UiLogicTests
    {
        [Theory]
        [InlineData(1234.5, "R1,234.50")]
        [InlineData(-1234.5, "-R1,234.50")]
        [InlineData(0, "R0.00")]
        public void Money_IsRandWithAPointAndCommaGroups(decimal amount, string expected) =>
            Format.Money(amount).Should().Be(expected);

        [Fact]
        public void Money_InAnotherCurrency_LeadsWithItsIsoCode() =>
            Format.Money(-75m, "USD").Should().Be("-USD 75.00");

        [Theory]
        [InlineData(250, "+R250.00")]
        [InlineData(-250, "-R250.00")]
        public void SignedMoney_AlwaysShowsTheDirection(decimal amount, string expected) =>
            Format.SignedMoney(amount).Should().Be(expected);

        [Theory]
        [InlineData(950, "R950")]
        [InlineData(12500, "R12.5k")]
        [InlineData(1_250_000, "R1.3m")]
        [InlineData(-4000, "-R4k")]
        public void CompactMoney_FitsAxisTicks(decimal amount, string expected) =>
            Format.CompactMoney(amount).Should().Be(expected);

        [Theory]
        [InlineData(12.5, "+12.5%")]
        [InlineData(-3, "-3%")]
        [InlineData(0, "0%")]
        public void Change_IsSigned(decimal percent, string expected) =>
            Format.Change(percent).Should().Be(expected);

        [Fact]
        public void BookingDate_IsTheSouthAfricanDay_TheServerAggregatesBy()
        {
            var lateUtc = new DateTime(2026, 6, 30, 22, 30, 0, DateTimeKind.Utc);

            Format.BookingDate(lateUtc).Should().Be("1 Jul 2026", "22:30 UTC is 00:30 the next day in South Africa");
        }

        [Theory]
        [InlineData("2026-02-29")]
        [InlineData("29/02/2026")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseDay_RejectsAnythingButARealIsoDay(string? value) =>
            Format.ParseDay(value).Should().BeNull();

        [Theory]
        [InlineData(ReportRange.ThisMonth, "2026-10-01", "2026-10-04")]
        [InlineData(ReportRange.LastThreeMonths, "2026-08-01", "2026-10-04")]
        [InlineData(ReportRange.YearToDate, "2026-01-01", "2026-10-04")]
        [InlineData(ReportRange.LastTwelveMonths, "2025-11-01", "2026-10-04")]
        public void ReportRange_PresetsStartOnAMonthBoundary_AndEndToday(string key, string from, string to)
        {
            var range = ReportRange.Resolve(key, new DateOnly(2026, 10, 4));

            range.Should().Be((DateOnly.Parse(from), DateOnly.Parse(to)));
        }

        [Fact]
        public void ReportRange_UnknownKey_IsNotAPreset() =>
            ReportRange.Resolve("forever", new DateOnly(2026, 10, 4)).Should().BeNull();

        [Fact]
        public void ApiQuery_DropsEmptyValues_SendsEnumsAsNumbers_AndEscapes()
        {
            var query = ApiQuery.Of(
                ("search", "Pick n Pay & co"),
                ("bank", null),
                ("account", ""),
                ("category", ui::TransactionAggregationUI.Models.Transactions.TransactionCategory.Dining),
                ("min", 12.5m));

            query.Should().Be("search=Pick%20n%20Pay%20%26%20co&category=2&min=12.5");
        }

        [Fact]
        public async Task AValidationProblem_IsShownAsItsFieldMessages_NotAStatusCode()
        {
            var response = Problem(HttpStatusCode.BadRequest, """
                {"title":"One or more validation errors occurred.","status":400,
                 "errors":{"reference":["reference must be 1-50 letters."],"name":["name must be 1-200 characters."]}}
                """);

            var message = await ApiClient.DescribeFailureAsync(response);

            message.Should().Be("reference must be 1-50 letters. name must be 1-200 characters.");
        }

        [Fact]
        public async Task AProblemWithADetail_IsShownAsTheDetail()
        {
            var response = Problem(HttpStatusCode.Conflict, """{"status":409,"detail":"A customer with reference 'CUST-1' already exists."}""");

            (await ApiClient.DescribeFailureAsync(response)).Should().Be("A customer with reference 'CUST-1' already exists.");
        }

        [Fact]
        public async Task ABodyThatIsNotJson_FallsBackToTheStatus()
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") };

            (await ApiClient.DescribeFailureAsync(response)).Should().Be("Request failed (502)");
        }

        private static HttpResponseMessage Problem(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") };
    }
}