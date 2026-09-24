using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TransactionAggregation.MockAggregator.Feed;

public interface IDeliveryChannel
{
    Task DeliverAsync(DeliveryBatch batch, CancellationToken cancellationToken);
}

/// <summary>POSTs to the application's bank-transactions webhook, authenticated by the source's API key.</summary>
public sealed class WebhookDelivery(HttpClient httpClient, IOptions<FeedOptions> options, ILogger<WebhookDelivery> logger)
    : IDeliveryChannel
{
    private const string Path = "api/v1/webhooks/bank-aggregator/transactions";

    public async Task DeliverAsync(DeliveryBatch batch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Path)
        {
            Content = JsonContent.Create(batch.Payload, options: JsonSerializerOptions.Web)
        };
        request.Headers.Add("X-Api-Key", options.Value.ApiKey);
        request.Headers.Add("Idempotency-Key", batch.IdempotencyKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Delivered {Count} transactions for {Account} ({Institution}) by webhook: {Status}",
                batch.Payload.Transactions.Count, batch.Account.Id, batch.Account.Institution, (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Webhook rejected the batch for {Account}: {Status} {Body}",
            batch.Account.Id, (int)response.StatusCode, body.Length <= 500 ? body : body[..500]);
    }
}

/// <summary>Produces to the application's bank-transactions topic, keyed by account so its batches stay ordered.</summary>
public sealed class KafkaDelivery : IDeliveryChannel, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly FeedOptions _options;
    private readonly ILogger<KafkaDelivery> _logger;

    public KafkaDelivery(IConfiguration configuration, IOptions<FeedOptions> options, ILogger<KafkaDelivery> logger)
    {
        var bootstrapServers = configuration.GetConnectionString("kafka")
            ?? throw new InvalidOperationException("Feed:Channel is Kafka but ConnectionStrings:kafka is not set.");
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true
        }).Build();
        _options = options.Value;
        _logger = logger;
    }

    public async Task DeliverAsync(DeliveryBatch batch, CancellationToken cancellationToken)
    {
        var message = new Message<string, string>
        {
            Key = batch.Account.Id,
            Value = JsonSerializer.Serialize(batch.Payload, JsonSerializerOptions.Web),
            Headers = new Headers
            {
                { "idempotency-key", Encoding.UTF8.GetBytes(batch.IdempotencyKey) },
                { "source", Encoding.UTF8.GetBytes(_options.SourceName) }
            }
        };

        var result = await _producer.ProduceAsync(_options.KafkaTopic, message, cancellationToken);
        _logger.LogInformation("Delivered {Count} transactions for {Account} ({Institution}) to Kafka {TopicPartitionOffset}",
            batch.Payload.Transactions.Count, batch.Account.Id, batch.Account.Institution, result.TopicPartitionOffset);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}