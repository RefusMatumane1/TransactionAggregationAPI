using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public partial class CurrencyConsistencyTests
    {
        private static readonly string[] ProductionRoots =
        [
            "Modules", "BuildingBlocks", "TransactionAggregationAPI", "TransactionAggregation.Hosting",
            "TransactionAggregation.Worker"
        ];

        [GeneratedRegex(@"""[A-Z]{3}""\s*,\s*""[A-Z]{3}""\s*,\s*""[A-Z]{3}""")]
        private static partial Regex ListOfCodes();

        [GeneratedRegex(@"""ZAR""")]
        private static partial Regex HardcodedBase();

        private static IEnumerable<(string Path, int Line, string Text)> ProductionSourceLines()
        {
            var root = FindRepositoryRoot();
            foreach (var dir in ProductionRoots)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
                {
                    var normalized = file.Replace('\\', '/');
                    if (normalized.Contains("/obj/") || normalized.Contains("/bin/") || normalized.Contains("/Migrations/"))
                        continue;

                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                        yield return (Path.GetRelativePath(root, file), i + 1, lines[i]);
                }
        }

        [Fact]
        public void NoProductionCode_KeepsItsOwnListOfCurrencyCodes()
        {
            var offenders = ProductionSourceLines()
                .Where(l => ListOfCodes().IsMatch(l.Text))
                .Select(l => $"{l.Path}:{l.Line}")
                .ToList();

            offenders.Should().BeEmpty("the supported currencies live in currencies.json, read through ICurrencyCatalog");
        }

        [Fact]
        public void NoProductionCode_HardcodesTheBaseCurrency()
        {
            var offenders = ProductionSourceLines()
                .Where(l => HardcodedBase().IsMatch(l.Text))
                .Select(l => $"{l.Path}:{l.Line}")
                .ToList();

            offenders.Should().BeEmpty("the base currency is Currencies:BaseCurrency; read ICurrencyCatalog.BaseCurrency");
        }

        [Fact]
        public void TheOldStaticList_IsGone()
        {
            typeof(SharedKernel.Common.ValueObjects.CurrencyCode).Assembly.GetTypes()
                .Should().NotContain(t => t.Name == "SupportedCurrency");
        }

        [Fact]
        public void TheShippedConfigurationFile_IsTheOnlyPlaceTheBaseIsNamed()
        {
            var json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "TransactionAggregation.Hosting", "currencies.json"));

            json.Should().Contain("\"BaseCurrency\"").And.Contain("\"Rates\"");
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TransactionAggregationAPI.slnx")))
                dir = dir.Parent;

            return dir?.FullName ?? throw new InvalidOperationException("Could not locate TransactionAggregationAPI.slnx above the test output directory.");
        }
    }
}