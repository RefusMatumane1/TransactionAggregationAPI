using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Application.Services;

namespace TransactionAggregation.Tests.Helpers
{
    public static class TestNormalizers
    {
        public static ITransactionNormalizer Neutral { get; } =
            new TransactionNormalizer(Options.Create(new NormalizationOptions()));

        public static ITransactionNormalizer Shipped() => new TransactionNormalizer(Options.Create(ShippedOptions()));

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
}