namespace BuildingBlocks.Messaging.Archiving
{
    public interface IMessageArchive
    {
        Task<int> ArchiveProcessedInboxAsync(DateTime processedBefore, int batchSize, CancellationToken cancellationToken = default);

        Task<int> ArchiveProcessedOutboxAsync(DateTime processedBefore, int batchSize, CancellationToken cancellationToken = default);
    }
}