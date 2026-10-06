using BuildingBlocks.Messaging.Archiving;
using Microsoft.Extensions.Options;
using Prometheus;

namespace TransactionAggregation.Worker.BackgroundServices
{
    public sealed class MessageArchiveBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<MessageArchiveOptions> options,
        TimeProvider time,
        ILogger<MessageArchiveBackgroundService> logger) : PollingBackgroundService(logger)
    {
        internal static readonly Counter ArchivedMessages = Metrics.CreateCounter(
            "messaging_archived_messages_total",
            "Processed inbox/outbox messages moved from the hot queue tables to their archive tables, by queue.",
            new CounterConfiguration { LabelNames = ["queue"] });

        private readonly MessageArchiveOptions _options = options.Value;

        protected override TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));

        protected override TimeSpan InitialDelay => TimeSpan.FromSeconds(30);

        protected override async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
        {
            var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-Math.Max(1, _options.RetainDays));

            var inbox = await DrainAsync("inbox", (archive, ct) => archive.ArchiveProcessedInboxAsync(cutoff, _options.BatchSize, ct), cancellationToken);
            var outbox = await DrainAsync("outbox", (archive, ct) => archive.ArchiveProcessedOutboxAsync(cutoff, _options.BatchSize, ct), cancellationToken);

            if (inbox + outbox > 0)
                logger.LogInformation(
                    "Archived {InboxCount} inbox and {OutboxCount} outbox messages processed before {Cutoff:O}",
                    inbox, outbox, cutoff);

            return false;
        }

        internal async Task<int> DrainAsync(
            string queue, Func<IMessageArchive, CancellationToken, Task<int>> archiveBatch, CancellationToken cancellationToken)
        {
            var total = 0;
            for (var batch = 0; batch < _options.MaxBatchesPerRun; batch++)
            {
                using var scope = scopeFactory.CreateScope();
                var moved = await archiveBatch(scope.ServiceProvider.GetRequiredService<IMessageArchive>(), cancellationToken);

                total += moved;
                ArchivedMessages.WithLabels(queue).Inc(moved);

                if (moved < _options.BatchSize)
                    break;
            }

            return total;
        }
    }
}