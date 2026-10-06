using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.WebhookSources.Application.Contracts;
using Modules.WebhookSources.Contracts;
using Modules.WebhookSources.Domain;
using NSubstitute;
using SharedKernel.Common.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregation.Worker.Kafka;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Kafka
{
    public sealed class KafkaContainerFixture : IAsyncLifetime
    {
        private IContainer _container = null!;

        public string BootstrapServers { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            // The broker advertises the host port clients use, so it is known before start.
            var hostPort = FreeTcpPort();
            _container = new ContainerBuilder("confluentinc/confluent-local:8.1.1")
                .WithPortBinding(hostPort, 9092)
                .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://localhost:29092,CONTROLLER://localhost:29093,PLAINTEXT_HOST://0.0.0.0:9092")
                .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT")
                .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://localhost:29092,PLAINTEXT_HOST://127.0.0.1:{hostPort}")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Kafka Server started"))
                .Build();
            await _container.StartAsync();
            BootstrapServers = $"127.0.0.1:{hostPort}";
        }

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();

        private static int FreeTcpPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }

    public class KafkaConsumerBrokerTests(KafkaContainerFixture broker) : IClassFixture<KafkaContainerFixture>
    {
        private const string Source = "broker-test-provider";
        private static readonly ECDsa ProviderKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        private sealed record Harness(
            ServiceProvider Services, KafkaOptions Options, ConcurrentQueue<ReceiveBankTransactionsCommand> Received, IAuditTrail AuditTrail);

        private Harness Build()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var options = new KafkaOptions
            {
                BankTransactionsTopic = $"bank-transactions-{suffix}",
                DeadLetterTopic = $"bank-transactions-{suffix}.dlq",
                GroupId = $"broker-test-{suffix}",
                TopicPartitions = 1,
                MaxRetryBackoffSeconds = 1
            };

            var received = new ConcurrentQueue<ReceiveBankTransactionsCommand>();
            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<ReceiveBankTransactionsCommand>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    received.Enqueue(call.Arg<ReceiveBankTransactionsCommand>());
                    return Result.Success(new InboxReceipt(Guid.NewGuid(), IsDuplicate: false));
                });
            var auditTrail = Substitute.For<IAuditTrail>();

            var sources = InMemoryWebhookSourcesDbContextFactory.Create();
            var source = WebhookSource.CreateWithProvisionedKey(Source, $"{Source}-provisioned-key-0123456789", Source, "#123456");
            source.RegisterSigningPublicKey(Convert.ToBase64String(ProviderKey.ExportSubjectPublicKeyInfo()));
            sources.WebhookSources.Add(source);
            sources.SaveChanges();

            var services = new ServiceCollection()
                .AddSingleton(sender)
                .AddSingleton(auditTrail)
                .AddSingleton<IWebhookSourceDirectory>(new WebhookSourceDirectory(sources))
                .AddSingleton(Options.Create(options))
                .AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>))
                .AddScoped<BankTransactionsKafkaMessageHandler>()
                .BuildServiceProvider();

            return new Harness(services, options, received, auditTrail);
        }

        private BankTransactionsKafkaConsumer Consumer(Harness harness) => new(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder()
                .AddInMemoryCollection([new($"ConnectionStrings:{KafkaOptions.ConnectionStringName}", broker.BootstrapServers)])
                .Build(),
            Options.Create(harness.Options),
            Substitute.For<IHostApplicationLifetime>(),
            NullLogger<BankTransactionsKafkaConsumer>.Instance);

        private static string Body(string id) =>
            $$"""{"externalAccountId":"broker-acc","transactions":[{"id":"{{id}}","amount":-10,"currency":"ZAR","description":"Broker test","date":"2026-09-30T10:00:00Z"}]}""";

        private static string Sign(string idempotencyKey, string value) => Convert.ToBase64String(ProviderKey.SignData(
            KafkaRecordSignature.SignedContent(Source, idempotencyKey, value),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        private async Task ProduceAsync(string topic, string idempotencyKey, string value, string? signature)
        {
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = broker.BootstrapServers }).Build();
            var headers = new Headers
            {
                { BankTransactionsKafkaMessageHandler.SourceHeader, Encoding.UTF8.GetBytes(Source) },
                { BankTransactionsKafkaMessageHandler.IdempotencyKeyHeader, Encoding.UTF8.GetBytes(idempotencyKey) }
            };
            if (signature is not null)
                headers.Add(BankTransactionsKafkaMessageHandler.SignatureHeader, Encoding.UTF8.GetBytes(signature));
            await producer.ProduceAsync(topic, new Message<string, string> { Key = "broker-acc", Value = value, Headers = headers });
        }

        private List<string> DeadLetterReasons(string topic, int expected, TimeSpan timeout)
        {
            using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = broker.BootstrapServers,
                GroupId = $"dlq-reader-{Guid.NewGuid():N}",
                AutoOffsetReset = AutoOffsetReset.Earliest
            }).Build();
            consumer.Subscribe(topic);
            var reasons = new List<string>();
            var deadline = DateTime.UtcNow + timeout;
            while (reasons.Count < expected && DateTime.UtcNow < deadline)
            {
                var record = consumer.Consume(TimeSpan.FromSeconds(1));
                if (record?.Message?.Headers?.TryGetLastBytes("dlq-reason", out var reason) == true)
                    reasons.Add(Encoding.UTF8.GetString(reason));
            }
            consumer.Close();
            return reasons;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(200);
        }

        [Fact]
        public async Task SignedRecordsAreAccepted_UnauthenticatedOnesAreDeadLettered_AndOffsetsAreCommitted()
        {
            var harness = Build();
            var topic = harness.Options.BankTransactionsTopic;

            var consumer = Consumer(harness);
            await consumer.StartAsync(CancellationToken.None);

            var valid = Body("broker-1");
            await ProduceAsync(topic, "d-1", valid, Sign("d-1", valid));
            await ProduceAsync(topic, "d-2", Body("broker-2"), signature: null);
            await ProduceAsync(topic, "d-3", Body("broker-3-changed"), Sign("d-3", Body("broker-3")));

            await WaitUntilAsync(() => harness.Received.Count >= 1, TimeSpan.FromSeconds(60));
            var reasons = DeadLetterReasons(harness.Options.DeadLetterTopic, expected: 2, TimeSpan.FromSeconds(60));
            await consumer.StopAsync(CancellationToken.None);

            harness.Received.Should().ContainSingle();
            harness.Received.Single().SourceName.Should().Be(Source);
            harness.Received.Single().IdempotencyKey.Should().Be("d-1");

            reasons.Should().HaveCount(2);
            reasons.Should().Contain(r => r.Contains($"Missing '{BankTransactionsKafkaMessageHandler.SignatureHeader}' header"));
            reasons.Should().Contain(r => r.Contains("Signature does not verify"));
            await harness.AuditTrail.Received(2).RecordAsync(
                Arg.Is<IReadOnlyCollection<AuditEventRecord>>(events => events.Single().SourceName == AuditSources.Unauthenticated),
                Arg.Any<CancellationToken>());

            // Same group: committed offsets mean nothing is redelivered.
            var restarted = Consumer(harness);
            await restarted.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(8));
            await restarted.StopAsync(CancellationToken.None);

            harness.Received.Should().ContainSingle("the accepted record's offset was committed before shutdown");
            await harness.AuditTrail.Received(2).RecordAsync(Arg.Any<IReadOnlyCollection<AuditEventRecord>>(), Arg.Any<CancellationToken>());
        }
    }
}