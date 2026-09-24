using TransactionAggregation.MockAggregator.Consent;
using TransactionAggregation.MockAggregator.Feed;

namespace TransactionAggregation.MockAggregator;

// An explicit, namespaced entry point rather than top-level statements: the generated
// top-level Program is a public global type, which would collide with the API's own
// Program in any assembly that references both (the test project does).
internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();

        builder.Services.Configure<MockAggregatorOptions>(builder.Configuration.GetSection(MockAggregatorOptions.SectionName));
        builder.Services.Configure<FeedOptions>(builder.Configuration.GetSection(FeedOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ConsentStore>();
        builder.Services.AddSingleton<TransactionGenerator>();

        var feed = builder.Configuration.GetSection(FeedOptions.SectionName).Get<FeedOptions>() ?? new FeedOptions();
        if (feed.Channel == DeliveryChannel.Kafka)
        {
            builder.Services.AddSingleton<IDeliveryChannel, KafkaDelivery>();
        }
        else
        {
            if (feed.Enabled && !Uri.TryCreate(feed.ApiBaseUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException(
                    "Feed:ApiBaseUrl must be the application API's absolute base URL when the webhook feed is enabled.");

            builder.Services.AddHttpClient<IDeliveryChannel, WebhookDelivery>(client =>
            {
                if (Uri.TryCreate(feed.ApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress))
                    client.BaseAddress = baseAddress;
            });
        }

        builder.Services.AddHostedService<FeedBackgroundService>();

        var app = builder.Build();

        app.MapDefaultEndpoints();
        app.MapOAuthEndpoints();
        app.MapGet("/", () => Results.Text(
            "Mock account aggregator (development only). Consent: /oauth/authorize. Consented accounts: GET /consents."));

        app.Run();
    }
}