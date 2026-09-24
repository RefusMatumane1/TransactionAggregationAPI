using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace TransactionAggregation.Tests.Architecture;

/// <summary>
/// Guards against personal data and secrets reaching log sinks. Serilog message-template
/// arguments are scalars, so the [Sensitive] destructuring policy can't redact them — the only
/// reliable control is to never name such a value in a template, and to never push whole
/// request objects into the log context.
/// </summary>
public partial class LoggingHygieneTests
{
    private static readonly string[] ProductionRoots =
    [
        "Modules", "BuildingBlocks", "TransactionAggregationAPI", "TransactionAggregation.Hosting",
        "TransactionAggregation.Worker"
    ];

    [GeneratedRegex(@"\{@?(Email|EmailAddress|Phone|PhoneNumber|FirstName|LastName|FullName|Password|ApiKey|Token|AccessToken|RefreshToken|ClientSecret|Iban|CardNumber)\}", RegexOptions.IgnoreCase)]
    private static partial Regex PiiPlaceholder();

    [GeneratedRegex(@"LogContext\.PushProperty\(\s*""Request""")]
    private static partial Regex WholeRequestInLogContext();

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
    public void NoLogTemplate_NamesPersonalDataOrSecrets()
    {
        var offenders = ProductionSourceLines()
            .Where(l => l.Text.Contains("Log") && PiiPlaceholder().IsMatch(l.Text))
            .Select(l => $"{l.Path}:{l.Line}")
            .ToList();

        offenders.Should().BeEmpty("log templates must identify people by id, never by email/name/phone or credentials");
    }

    [Fact]
    public void NoCode_PushesWholeRequestObjectsIntoTheLogContext()
    {
        var offenders = ProductionSourceLines()
            .Where(l => WholeRequestInLogContext().IsMatch(l.Text))
            .Select(l => $"{l.Path}:{l.Line}")
            .ToList();

        offenders.Should().BeEmpty("a pushed request is stamped on every event in scope, carrying PII and transaction data with it");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TransactionAggregationAPI.slnx")))
            dir = dir.Parent;

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate TransactionAggregationAPI.slnx above the test output directory.");
    }
}