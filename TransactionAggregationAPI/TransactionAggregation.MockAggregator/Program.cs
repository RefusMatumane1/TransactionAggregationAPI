using TransactionAggregation.MockAggregator.Feed;

namespace TransactionAggregation.MockAggregator
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddSecretsFile();

            builder.AddServiceDefaults();

            builder.Services.Configure<MockAggregatorOptions>(builder.Configuration.GetSection(MockAggregatorOptions.SectionName));
            builder.Services.Configure<FeedOptions>(builder.Configuration.GetSection(FeedOptions.SectionName));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<TransactionGenerator>();
            builder.Services.AddSingleton<RecordSigningKey>();

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
                })
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
            }

            builder.Services.AddHostedService<FeedBackgroundService>();

            var app = builder.Build();

            app.MapDefaultEndpoints();
            app.MapGet("/kafka/signing-key", (RecordSigningKey key) => Results.Ok(new
            {
                publicKey = key.PublicKey,
                algorithm = "ECDSA P-256, SHA-256, IEEE P1363 signature",
                format = RecordSigningKey.SignatureVersion
            }));
            app.MapGet("/", () => Results.Text(
                "Mock account aggregator (development only). Pushes transactions for every catalog account to the API."));

            app.Run();
        }
    }
}