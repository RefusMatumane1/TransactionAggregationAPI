using BuildingBlocks.Messaging.Publishing;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using TransactionAggregation.Worker.Kafka;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Kafka
{
    public class KafkaIntegrationEventPublisherTests(KafkaContainerFixture broker) : IClassFixture<KafkaContainerFixture>
    {
        [Fact]
        public async Task Publish_DeliversTheEvent_KeyedByAccount_WithTheHeadersConsumersDedupeAndTraceOn()
        {
            var topic = $"transaction-events-{Guid.NewGuid():N}";
            using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker.BootstrapServers }).Build())
                await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:kafka"] = broker.BootstrapServers })
                .Build();
            using var publisher = new KafkaIntegrationEventPublisher(configuration, Options.Create(new KafkaOptions { IntegrationEventsTopic = topic }));
            var envelope = new IntegrationEventEnvelope(
                MessageId: Guid.NewGuid(),
                Type: "TransactionRecorded",
                SchemaVersion: 1,
                Payload: """{"transactionId":"x"}""",
                PartitionKey: "FNB:acc-1",
                OccurredAt: new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc),
                TraceParent: "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
                CorrelationId: "corr-1");

            await publisher.PublishAsync(envelope, CancellationToken.None);

            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = broker.BootstrapServers,
                GroupId = $"publisher-test-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest
            }).Build();
            consumer.Subscribe(topic);
            var record = consumer.Consume(TimeSpan.FromSeconds(30));
            consumer.Close();

            record.Should().NotBeNull();
            record!.Message.Key.Should().Be("FNB:acc-1");
            record.Message.Value.Should().Be(envelope.Payload);
            string Header(string name) => Encoding.UTF8.GetString(record.Message.Headers.GetLastBytes(name));
            Header(KafkaIntegrationEventPublisher.MessageIdHeader).Should().Be(envelope.MessageId.ToString());
            Header(KafkaIntegrationEventPublisher.EventTypeHeader).Should().Be("TransactionRecorded");
            Header(KafkaIntegrationEventPublisher.SchemaVersionHeader).Should().Be("1");
            Header(KafkaIntegrationEventPublisher.TraceParentHeader).Should().Be(envelope.TraceParent);
            Header(KafkaIntegrationEventPublisher.CorrelationIdHeader).Should().Be("corr-1");
        }
    }
}