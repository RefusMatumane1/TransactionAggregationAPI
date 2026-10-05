using BuildingBlocks.Messaging;

namespace TransactionAggregation.Worker.Kafka
{
    internal sealed class KafkaRecordSettler(KafkaOptions options, ILogger logger)
    {
        public delegate Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);

        public async Task<KafkaMessageResult> SettleAsync(
            string recordDescription,
            Func<CancellationToken, Task<KafkaMessageResult>> handle,
            Func<string, CancellationToken, Task<KafkaMessageResult>> deadLetter,
            Func<string, CancellationToken, Task> publishDeadLetter,
            DelayAsync delay,
            CancellationToken cancellationToken)
        {
            var maxBackoff = TimeSpan.FromSeconds(Math.Max(1, options.MaxRetryBackoffSeconds));
            var maxUnclassified = Math.Max(1, options.MaxUnclassifiedAttempts);
            var attempt = 0;
            var unclassifiedFailures = 0;
            string? rejection = null;
            KafkaMessageResult? settled = null;

            while (true)
            {
                try
                {
                    settled ??= rejection is null
                        ? await handle(cancellationToken)
                        : await deadLetter(rejection, cancellationToken);

                    if (settled.Outcome == KafkaMessageOutcome.Rejected)
                        await publishDeadLetter(settled.Reason ?? rejection ?? "Rejected", cancellationToken);

                    return settled;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var reason = FailureClassifier.Describe(ex);

                    if (settled is null && rejection is null)
                    {
                        if (FailureClassifier.Classify(ex) == FailureKind.Permanent)
                        {
                            KafkaMetrics.PermanentFailures.Inc();
                            logger.LogWarning(ex, "Kafka record {Record} failed permanently ({Reason}) — dead-lettering", recordDescription, reason);
                            rejection = $"Permanent failure: {reason}";
                            continue;
                        }

                        if (!FailureClassifier.IsInfrastructureOutage(ex) && ++unclassifiedFailures >= maxUnclassified)
                        {
                            KafkaMetrics.PermanentFailures.Inc();
                            logger.LogError(ex, "Kafka record {Record} failed {Attempts} times with an unrecognised error — dead-lettering",
                                recordDescription, unclassifiedFailures);
                            rejection = $"Gave up after {unclassifiedFailures} attempts: {reason}";
                            continue;
                        }
                    }

                    attempt++;
                    var backoff = Jittered(attempt, maxBackoff);
                    KafkaMetrics.TransientFailures.Inc();
                    logger.LogError(ex, "Failed to settle Kafka record {Record} (attempt {Attempt}: {Reason}) — retrying in {Backoff}",
                        recordDescription, attempt, reason, backoff);

                    await delay(backoff, cancellationToken);
                }
            }
        }

        private static TimeSpan Jittered(int attempt, TimeSpan max)
        {
            var ceiling = Math.Min(Math.Pow(2, Math.Min(attempt - 1, 20)), max.TotalSeconds);
            return TimeSpan.FromSeconds(ceiling / 2 + Random.Shared.NextDouble() * ceiling / 2);
        }
    }
}