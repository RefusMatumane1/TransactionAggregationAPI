using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Services;

namespace TransactionAggregation.Tests.Helpers;

/// <summary>Real TransactionNormalizer instances for tests that build the ingestion handler by hand.</summary>
public static class TestNormalizers
{
    /// <summary>
    /// UTC, no description prefixes, no category map: normalization changes nothing a test
    /// didn't ask for, so tests about other stages keep asserting on exactly what they sent.
    /// </summary>
    public static ITransactionNormalizer Neutral { get; } =
        new TransactionNormalizer(Options.Create(new NormalizationOptions()));

    /// <summary>The rules actually shipped in normalization-rules.json.</summary>
    public static ITransactionNormalizer Shipped() => new TransactionNormalizer(Options.Create(ShippedOptions()));

    /// <summary>The shipped rules plus an environment's overlay, layered the way the hosts load them.</summary>
    public static ITransactionNormalizer ShippedFor(string environment) =>
        new TransactionNormalizer(Options.Create(ShippedOptions(environment)));

    public static NormalizationOptions ShippedOptions(string? environment = null)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "normalization-rules.json"), optional: false);
        if (environment is not null)
            builder.AddJsonFile(Path.Combine(AppContext.BaseDirectory, $"normalization-rules.{environment}.json"), optional: false);
        var configuration = builder.Build();

        var options = new NormalizationOptions();
        configuration.GetSection(NormalizationOptions.SectionName).Bind(options);
        return options;
    }
}