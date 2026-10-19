using BuildingBlocks.Application.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace Modules.WebhookSources.Infrastructure.Development
{
    /// <summary>Registers one webhook source per bank the mock aggregator impersonates, with keys derived from the shared mock key.</summary>
    public sealed class MockAggregatorBankSeeder(
        WebhookSourcesDbContext context,
        IConfiguration configuration,
        ILogger<MockAggregatorBankSeeder> logger) : IDevelopmentSeeder
    {
        public const string ConfigurationSection = "MockAggregator";

        public sealed record MockBank(string Code, string DisplayName, string Color);

        public static readonly MockBank[] Banks =
        [
            new("FNB", "FNB", "#00A3AD"),
            new("StandardBank", "Standard Bank", "#0033A1"),
            new("Absa", "Absa", "#DC0032"),
            new("Capitec", "Capitec", "#1C3A70")
        ];

        public static string KeyFor(string baseKey, string code) => $"{baseKey}-{code.ToLowerInvariant()}";

        public async Task SeedAsync(CancellationToken cancellationToken)
        {
            var baseKey = configuration.GetSection(ConfigurationSection)["WebhookApiKey"];
            if (string.IsNullOrWhiteSpace(baseKey))
                return;

            var existing = await context.WebhookSources.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var bank in Banks)
            {
                var key = KeyFor(baseKey, bank.Code);
                if (!existing.TryGetValue(bank.Code, out var source))
                {
                    context.WebhookSources.Add(WebhookSource.CreateWithProvisionedKey(bank.Code, key, bank.DisplayName, bank.Color));
                    logger.LogInformation("Registered bank source {SourceName} for the mock aggregator", bank.Code);
                    continue;
                }

                if (!source.HasKey(key))
                    source.UseProvisionedKey(key);
                if (!source.IsActive)
                    source.Activate();
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
