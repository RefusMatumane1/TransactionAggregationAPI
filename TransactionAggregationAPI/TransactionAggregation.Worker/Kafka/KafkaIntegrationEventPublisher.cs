using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Publishing;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace TransactionAggregation.Worker.Kafka
{
    // One long-lived idempotent producer for the process (connections are reused, not opened per
    // message). acks=all + idempotence means a retried produce never duplicates within the broker;
    // a duplicate after a lost outbox acknowledgement is left to consumers, who dedupe on message-id.
    internal sealed class KafkaIntegrationEventPublisher : IIntegrationEventPublisher, IDisposable
    {
        public const string MessageIdHeader = "message-id";
        public const string EventTypeHeader = "event-type";
        public const string SchemaVersionHeader = "schema-version";
        public const string OccurredAtHeader = "occurred-at";
        public const string TraceParentHeader = "traceparent";
        public const string CorrelationIdHeader = "correlation-id";

        private readonly IProducer<string, string> _producer;
        private readonly string _topic;

        public KafkaIntegrationEventPublisher(IConfiguration configuration, IOptions<KafkaOptions> options)
        {
            _topic = options.Value.IntegrationEventsTopic;
            _producer = new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = configuration.GetConnectionString(KafkaOptions.ConnectionStringName),
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = (int)TimeSpan.FromSeconds(30).TotalMilliseconds
            }).Build();
        }

        public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        {
            var headers = new Headers
            {
                { MessageIdHeader, Encoding.UTF8.GetBytes(envelope.MessageId.ToString()) },
                { EventTypeHeader, Encoding.UTF8.GetBytes(envelope.Type) },
                { SchemaVersionHeader, Encoding.UTF8.GetBytes(envelope.SchemaVersion.ToString(CultureInfo.InvariantCulture)) },
                { OccurredAtHeader, Encoding.UTF8.GetBytes(envelope.OccurredAt.ToString("O", CultureInfo.InvariantCulture)) }
            };
            if (envelope.TraceParent is not null)
                headers.Add(TraceParentHeader, Encoding.UTF8.GetBytes(envelope.TraceParent));
            if (envelope.CorrelationId is not null)
                headers.Add(CorrelationIdHeader, Encoding.UTF8.GetBytes(envelope.CorrelationId));

            try
            {
                await _producer.ProduceAsync(
                    _topic,
                    new Message<string, string> { Key = envelope.PartitionKey, Value = envelope.Payload, Headers = headers },
                    cancellationToken);
            }
            catch (ProduceException<string, string> ex) when (IsPermanent(ex.Error))
            {
                throw new PermanentDeliveryException($"Kafka refused {envelope.Type} {envelope.MessageId}: {ex.Error.Reason}", ex);
            }
        }

        // Errors a retry cannot fix; everything else (broker unavailable, timeouts, leader changes)
        // is transient and left to the outbox's retry with backoff.
        private static bool IsPermanent(Error error) =>
            error.IsFatal
            || error.Code is ErrorCode.MsgSizeTooLarge
                or ErrorCode.TopicAuthorizationFailed
                or ErrorCode.ClusterAuthorizationFailed
                or ErrorCode.InvalidMsg;

        public void Dispose()
        {
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }
}