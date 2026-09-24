using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Modules.Transactions.Infrastructure.Kafka
{
    /// <summary>
    /// At-least-once consumer for the bank-transactions topic. A record's offset is stored
    /// (and later auto-committed) only after it has been durably written to the inbox or
    /// parked on the dead-letter topic, so a crash or rebalance redelivers it — and the
    /// redelivery lands on the same inbox row through the idempotency key rather than
    /// creating a second one.
    ///
    /// Transient failures (Postgres unavailable, DLQ produce failing) retry the same record
    /// with backoff and deliberately stall its partition: skipping ahead would commit past
    /// a record that was never stored.
    /// </summary>
    internal sealed class BankTransactionsKafkaConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<KafkaOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<BankTransactionsKafkaConsumer> logger) : BackgroundService
    {
        private const string DeadLetterReasonHeader = "dlq-reason";
        private const string OriginalTopicHeader = "dlq-original-topic";
        private const string OriginalPartitionHeader = "dlq-original-partition";
        private const string OriginalOffsetHeader = "dlq-original-offset";

        private readonly KafkaOptions _options = options.Value;
        private readonly string _bootstrapServers =
            configuration.GetConnectionString(KafkaOptions.ConnectionStringName) ?? string.Empty;

        // Consume() blocks its thread; running it on the thread pool from ExecuteAsync would
        // also block host startup until the first await.
        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.Factory.StartNew(
                () => RunAsync(stoppingToken),
                stoppingToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (_options.CreateTopics)
                    await EnsureTopicsExistAsync(stoppingToken);

                using var consumer = BuildConsumer();
                using var deadLetterProducer = BuildDeadLetterProducer();

                consumer.Subscribe(_options.BankTransactionsTopic);
                logger.LogInformation(
                    "Kafka consumer subscribed to {Topic} (group {GroupId}, source {SourceName})",
                    _options.BankTransactionsTopic, _options.GroupId, _options.SourceName);

                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        ConsumeResult<string?, string?> record;
                        try
                        {
                            record = consumer.Consume(stoppingToken);
                        }
                        catch (ConsumeException ex) when (!ex.Error.IsFatal)
                        {
                            logger.LogWarning(ex, "Kafka consume error on {Topic}: {Reason}", _options.BankTransactionsTopic, ex.Error.Reason);
                            continue;
                        }

                        if (record?.Message is null)
                            continue;

                        await ProcessUntilSettledAsync(record, deadLetterProducer, stoppingToken);
                        consumer.StoreOffset(record);
                    }
                }
                finally
                {
                    // Commits stored offsets and leaves the group cleanly so partitions are
                    // reassigned immediately rather than after the session timeout.
                    consumer.Close();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                // A worker whose consumer has died would otherwise keep running, stay green on
                // /alive and silently stop ingesting. Stopping the host with a failure exit
                // code hands recovery to the orchestrator (k8s/compose restart the process),
                // which re-joins the consumer group from the last committed offset.
                KafkaMetrics.ConsumerCrashes.Inc();
                logger.LogCritical(ex, "Kafka consumer for {Topic} stopped unexpectedly — stopping the host so it is restarted",
                    _options.BankTransactionsTopic);
                Environment.ExitCode = 1;
                lifetime.StopApplication();
            }
        }

        private async Task ProcessUntilSettledAsync(
            ConsumeResult<string?, string?> record,
            IProducer<string?, string?> deadLetterProducer,
            CancellationToken stoppingToken)
        {
            var backoff = TimeSpan.FromSeconds(1);
            var maxBackoff = TimeSpan.FromSeconds(Math.Max(1, _options.MaxRetryBackoffSeconds));

            while (true)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var handler = scope.ServiceProvider.GetRequiredService<BankTransactionsKafkaMessageHandler>();

                    var timestamp = record.Message.Timestamp;
                    var result = await handler.HandleAsync(
                        new KafkaInboundRecord(
                            record.Topic,
                            record.Partition.Value,
                            record.Offset.Value,
                            record.Message.Key,
                            record.Message.Value,
                            ReadHeader(record.Message.Headers, BankTransactionsKafkaMessageHandler.IdempotencyKeyHeader),
                            timestamp.Type == TimestampType.NotAvailable ? null : timestamp.UtcDateTime,
                            ReadHeader(record.Message.Headers, BankTransactionsKafkaMessageHandler.SourceHeader)),
                        stoppingToken);

                    switch (result.Outcome)
                    {
                        case KafkaMessageOutcome.Rejected:
                            await PublishDeadLetterAsync(record, result.Reason ?? "Rejected", deadLetterProducer, stoppingToken);
                            KafkaMetrics.MessagesConsumed.WithLabels("dead_lettered").Inc();
                            logger.LogWarning(
                                "Kafka record {TopicPartitionOffset} rejected and moved to {DeadLetterTopic}: {Reason}",
                                record.TopicPartitionOffset, _options.DeadLetterTopic, result.Reason);
                            break;

                        case KafkaMessageOutcome.Duplicate:
                            KafkaMetrics.MessagesConsumed.WithLabels("duplicate").Inc();
                            logger.LogInformation(
                                "Kafka record {TopicPartitionOffset} is a duplicate of inbox message {InboxMessageId} — skipped",
                                record.TopicPartitionOffset, result.InboxMessageId);
                            break;

                        default:
                            KafkaMetrics.MessagesConsumed.WithLabels("enqueued").Inc();
                            logger.LogInformation(
                                "Kafka record {TopicPartitionOffset} stored as inbox message {InboxMessageId}",
                                record.TopicPartitionOffset, result.InboxMessageId);
                            break;
                    }

                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    KafkaMetrics.TransientFailures.Inc();
                    logger.LogError(ex,
                        "Failed to store Kafka record {TopicPartitionOffset} — retrying in {Backoff}",
                        record.TopicPartitionOffset, backoff);

                    await Task.Delay(backoff, stoppingToken);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, maxBackoff.Ticks));
                }
            }
        }

        private async Task PublishDeadLetterAsync(
            ConsumeResult<string?, string?> record,
            string reason,
            IProducer<string?, string?> producer,
            CancellationToken cancellationToken)
        {
            var headers = new Headers();
            foreach (var header in record.Message.Headers ?? [])
                headers.Add(header.Key, header.GetValueBytes());

            headers.Add(DeadLetterReasonHeader, Encoding.UTF8.GetBytes(Truncate(reason, 1000)));
            headers.Add(OriginalTopicHeader, Encoding.UTF8.GetBytes(record.Topic));
            headers.Add(OriginalPartitionHeader, Encoding.UTF8.GetBytes(record.Partition.Value.ToString()));
            headers.Add(OriginalOffsetHeader, Encoding.UTF8.GetBytes(record.Offset.Value.ToString()));

            await producer.ProduceAsync(
                _options.DeadLetterTopic,
                new Message<string?, string?> { Key = record.Message.Key, Value = record.Message.Value, Headers = headers },
                cancellationToken);
        }

        private async Task EnsureTopicsExistAsync(CancellationToken stoppingToken)
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = _bootstrapServers }).Build();

            var topics = new[] { _options.BankTransactionsTopic, _options.DeadLetterTopic }
                .Select(name => new TopicSpecification
                {
                    Name = name,
                    NumPartitions = _options.TopicPartitions,
                    ReplicationFactor = _options.TopicReplicationFactor
                })
                .ToList();

            while (true)
            {
                try
                {
                    await admin.CreateTopicsAsync(topics, new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                    logger.LogInformation("Created Kafka topics {Topics}", topics.Select(t => t.Name));
                    return;
                }
                catch (CreateTopicsException ex) when (ex.Results.All(r =>
                    r.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
                {
                    return;
                }
                catch (KafkaException ex)
                {
                    // The broker may still be starting (compose/Aspire start the API alongside it).
                    logger.LogWarning("Kafka not reachable yet at {BootstrapServers} ({Reason}) — retrying topic setup",
                        _bootstrapServers, ex.Error.Reason);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private IConsumer<string?, string?> BuildConsumer() =>
            new ConsumerBuilder<string?, string?>(new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = _options.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                // Offsets are stored manually after a record is settled; the background
                // auto-commit then only ever commits settled offsets.
                EnableAutoCommit = true,
                EnableAutoOffsetStore = false,
                PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
            })
                .SetErrorHandler((_, error) => logger.LogWarning("Kafka consumer error: {Reason} (fatal: {IsFatal})", error.Reason, error.IsFatal))
                .Build();

        private IProducer<string?, string?> BuildDeadLetterProducer() =>
            new ProducerBuilder<string?, string?>(new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build();

        private static string? ReadHeader(Headers? headers, string key) =>
            headers is not null && headers.TryGetLastBytes(key, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}