// The UI is referenced under the "ui" alias: it has its own top-level Program, like the API.
extern alias ui;

using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.RegularExpressions;
using ui::TransactionAggregationUI.Models.Aggregates;
using ui::TransactionAggregationUI.Shared;
using Xunit;

namespace TransactionAggregation.Tests.Unit.UI
{
    // Real components rendered to HTML by Blazor's own HtmlRenderer: what a user (and a screen
    // reader) is given for a set of inputs.
    public partial class UiComponentTests
    {
        private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters) where TComponent : IComponent
        {
            await using var services = new ServiceCollection().BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
            return await renderer.Dispatcher.InvokeAsync(async () =>
                (await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        }

        private static CashFlowPointModel Month(int month, decimal income, decimal expenses, int count) => new()
        {
            PeriodStart = new DateOnly(2026, month, 1),
            PeriodEnd = new DateOnly(2026, month, 1).AddMonths(1).AddDays(-1),
            Income = income,
            Expenses = expenses,
            Net = income - expenses,
            TransactionCount = count
        };

        [GeneratedRegex("""<path class="bar-(income|expense)" d="(?<d>[^"]*)""")]
        private static partial Regex Bar();

        [Fact]
        public async Task CashFlowChart_DrawsAPairOfColumnsPerMonth_AndNothingForAnEmptyOne()
        {
            var html = await RenderAsync<CashFlowChart>(new()
            {
                ["Points"] = new List<CashFlowPointModel> { Month(1, 30_000m, 12_000m, 40), Month(2, 0m, 0m, 0), Month(3, 28_000m, 15_500m, 52) }
            });

            var drawn = Bar().Matches(html).Count(m => m.Groups["d"].Value.Length > 0);
            drawn.Should().Be(4, "two months with money, one column each for income and expenses; the empty month draws none");
            html.Should().Contain("Income").And.Contain("Expenses", "two series always carry a legend");
        }

        [Fact]
        public async Task CashFlowChart_HasRoundAxisTicks_AndATextAlternative()
        {
            var html = await RenderAsync<CashFlowChart>(new()
            {
                ["Points"] = new List<CashFlowPointModel> { Month(1, 30_000m, 12_000m, 40) }
            });

            html.Should().Contain(">R0<").And.Contain(">R30k<", "ticks step in round numbers up to the peak");
            html.Should().MatchRegex("role=\"img\" aria-label=\"Monthly income and expenses, 1 months\\. Income R30,000\\.00, expenses R12,000\\.00\\.\"");
        }

        [Fact]
        public async Task CashFlowChart_ColumnsNeverExceed24px_EvenWithOneMonth()
        {
            var html = await RenderAsync<CashFlowChart>(new()
            {
                ["Points"] = new List<CashFlowPointModel> { Month(1, 30_000m, 12_000m, 40) }
            });

            // Each column path runs H(left + r) ... H(right - r): its width is the H span plus 2r.
            foreach (Match bar in Bar().Matches(html))
            {
                var xs = Regex.Matches(bar.Groups["d"].Value, @"H([\d.]+)").Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
                var starts = Regex.Match(bar.Groups["d"].Value, @"^M([\d.]+),").Groups[1].Value;
                var left = double.Parse(starts, System.Globalization.CultureInfo.InvariantCulture);
                var right = xs[0] + 4;
                (right - left).Should().BeLessThanOrEqualTo(24.01);
            }
        }

        [Fact]
        public async Task Kpi_RisingSpending_IsMarkedBad_WithAnArrowAndWords_NotColourAlone()
        {
            var html = await RenderAsync<Kpi>(new()
            {
                ["Label"] = "Expenses",
                ["Value"] = "R12,000.00",
                ["Change"] = 12.5m,
                ["HigherIsBetter"] = false,
                ["HasComparison"] = true
            });

            html.Should().Contain("text-bad").And.Contain("bi-arrow-up-right");
            System.Net.WebUtility.HtmlDecode(html).Should().Contain("+12.5%").And.Contain("vs previous period");
        }

        [Fact]
        public async Task Kpi_RisingIncome_IsMarkedGood()
        {
            var html = await RenderAsync<Kpi>(new()
            {
                ["Label"] = "Income",
                ["Value"] = "R30,000.00",
                ["Change"] = 4m,
                ["HigherIsBetter"] = true,
                ["HasComparison"] = true
            });

            html.Should().Contain("text-good").And.NotContain("text-bad");
        }

        [Fact]
        public async Task Kpi_WithNothingToCompareAgainst_SaysSo()
        {
            var html = await RenderAsync<Kpi>(new()
            {
                ["Label"] = "Income",
                ["Value"] = "R30,000.00",
                ["Change"] = null,
                ["HigherIsBetter"] = true,
                ["HasComparison"] = true
            });

            html.Should().Contain("No spending or income in the previous period").And.NotContain("text-good");
        }
    }
}