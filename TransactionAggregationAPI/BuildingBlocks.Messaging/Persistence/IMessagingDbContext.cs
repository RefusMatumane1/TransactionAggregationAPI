using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Outbox;

namespace BuildingBlocks.Messaging.Persistence
{
    public interface IMessagingDbContext
    {
        DbSet<InboxMessage> InboxMessages { get; }
        DbSet<OutboxMessage> OutboxMessages { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, CancellationToken cancellationToken = default);

        Task<List<InboxMessage>> ClaimInboxMessagesAsync(
            int batchSize, TimeSpan claimTimeout, CancellationToken cancellationToken = default);
    }
}
