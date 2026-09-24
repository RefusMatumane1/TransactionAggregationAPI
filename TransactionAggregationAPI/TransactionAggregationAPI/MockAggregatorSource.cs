using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace TransactionAggregationAPI;

/// <summary>
/// Development only. Registers the webhook source the mock aggregator
/// (TransactionAggregation.MockAggregator) sends as, with the API key both apps are given
/// in configuration — so the mock bank feeds reach the real ingestion pipeline without an
/// admin creating a source and copying its one-time key by hand. Runs on every startup
/// (unlike SeedData) so a changed key or a deactivated source is put right.
/// </summary>
public static class MockAggregatorSource
{
    public const string ConfigurationSection = "MockAggregator";

    public static async Task EnsureRegisteredAsync(IServiceProvider services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSection);
        var apiKey = section["WebhookApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return; // no mock aggregator configured for this environment

        var sourceName = section["SourceName"] ?? "mock-aggregator";

        // The mock aggregator fronts every mock bank, so by default it may deliver for all of them.
        var institutions = section.GetSection("AuthorizedInstitutions").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : Enum.GetNames<Institution>();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhookSourcesDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        var source = await db.WebhookSources.FirstOrDefaultAsync(s => s.Name == sourceName);
        if (source is null)
        {
            db.WebhookSources.Add(WebhookSource.CreateWithProvisionedKey(sourceName, apiKey, institutions));
            logger.LogInformation("Registered webhook source {SourceName} for the mock aggregator", sourceName);
        }
        else
        {
            if (!source.HasKey(apiKey))
            {
                source.UseProvisionedKey(apiKey);
                logger.LogInformation("Updated the API key of webhook source {SourceName} to the configured one", sourceName);
            }
            if (!source.IsActive)
                source.Activate();
            if (source.AuthorizedInstitutions.Count != institutions.Length || !institutions.All(source.IsAuthorizedFor))
                source.AuthorizeInstitutions(institutions);
        }

        await db.SaveChangesAsync();
    }
}