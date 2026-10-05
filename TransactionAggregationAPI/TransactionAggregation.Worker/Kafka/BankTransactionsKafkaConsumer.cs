using BuildingBlocks.Messaging.Observability;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using System.Text;

namespace TransactionAggregation.Worker.Kafka
{
    internal sealed class BankTransactionsKafkaConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<KafkaOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<BankTransactionsKafkaConsumer> logger) : BackgroundService
    {
        private const string TraceParentHeader = "traceparent";
        private const string DeadLetterReasonHeader = "dlq-reason";
        private const string OriginalTopicHeader = "dlq-original-topic";
        private const string OriginalPartitionHeader = "dlq-original-partition";
        private const string OriginalOffsetHeader = "dlq-original-offset";

        private readonly KafkaOptions _options = options.Value;
        private readonly KafkaRecordSettler _settler = new(options.Value, logger);
        private readonly string _bootstrapServers =
            configuration.GetConnectionString(KafkaOptions.ConnectionStringName) ?? string.Empty;

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
                    "Kafka consumer subscribed to {Topic} (group {GroupId}); records must be signed by a registered source",
                    _options.BankTransactionsTopic, _options.GroupId);

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
                    consumer.Close();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
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
            using var activity = MessagingTelemetry.StartConsumerActivity(
                "kafka.bank-transactions.consume", ReadHeader(record.Message.Headers, TraceParentHeader));
            activity?.SetTag("messaging.system", "kafka");
            activity?.SetTag("messaging.destination.name", record.Topic);
            activity?.SetTag("messaging.kafka.partition", record.Partition.Value);
            activity?.SetTag("messaging.kafka.offset", record.Offset.Value);

            var timestamp = record.Message.Timestamp;
            var inbound = new KafkaInboundRecord(
                record.Topic,
                record.Partition.Value,
                record.Offset.Value,
                record.Message.Key,
                record.Message.Value,
                ReadHeader(record.Message.Headers, BankTransactionsKafkaMessageHandler.IdempotencyKeyHeader),
                timestamp.Type == TimestampType.NotAvailable ? null : timestamp.UtcDateTime,
                ReadHeader(record.Message.Headers, BankTransactionsKafkaMessageHandler.SourceHeader),
                ReadHeader(record.Message.Headers, BankTransactionsKafkaMessageHandler.SignatureHeader));

            var result = await _settler.SettleAsync(
                record.TopicPartitionOffset.ToString(),
                ct => WithHandlerAsync(handler => handler.HandleAsync(inbound, ct)),
                (reason, ct) => WithHandlerAsync(handler => handler.DeadLetterAsync(inbound, reason, ct)),
                (reason, ct) => PublishDeadLetterAsync(record, reason, deadLetterProducer, ct),
                Task.Delay,
                stoppingToken);

            switch (result.Outcome)
            {
                case KafkaMessageOutcome.Rejected:
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
        }

        private async Task<KafkaMessageResult> WithHandlerAsync(Func<BankTransactionsKafkaMessageHandler, Task<KafkaMessageResult>> action)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<BankTransactionsKafkaMessageHandler>());
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

            var topics = new[] { _options.BankTransactionsTopic, _options.DeadLetterTopic, _options.IntegrationEventsTopic }
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