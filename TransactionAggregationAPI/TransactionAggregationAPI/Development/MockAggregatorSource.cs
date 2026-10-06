using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace TransactionAggregationAPI.Development
{
    public static class MockAggregatorSource
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

        public static async Task EnsureRegisteredAsync(IServiceProvider services, IConfiguration configuration)
        {
            var baseKey = configuration.GetSection(ConfigurationSection)["WebhookApiKey"];
            if (string.IsNullOrWhiteSpace(baseKey))
                return;

            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WebhookSourcesDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var existing = await db.WebhookSources.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var bank in Banks)
            {
                var key = KeyFor(baseKey, bank.Code);
                if (!existing.TryGetValue(bank.Code, out var source))
                {
                    db.WebhookSources.Add(WebhookSource.CreateWithProvisionedKey(bank.Code, key, bank.DisplayName, bank.Color));
                    logger.LogInformation("Registered bank source {SourceName} for the mock aggregator", bank.Code);
                    continue;
                }

                if (!source.HasKey(key))
                    source.UseProvisionedKey(key);
                if (!source.IsActive)
                    source.Activate();
            }

            await db.SaveChangesAsync();
        }
    }
}