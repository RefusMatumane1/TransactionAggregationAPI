
using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Inbox;
using TransactionAggregation.Domain.Outbox;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Transaction> Transactions { get; }
        DbSet<Customer> Customers { get; }
        DbSet<Account> Accounts { get; }
        DbSet<BankLink> BankLinks { get; }
        DbSet<WebhookSource> WebhookSources { get; }
        DbSet<OutboxMessage> OutboxMessages { get; }
        DbSet<InboxMessage> InboxMessages { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically claims up to <paramref name="batchSize"/> due, pending outbox messages
        /// (flips them to Processing) using a provider-specific locking claim query — Postgres
        /// `FOR UPDATE SKIP LOCKED` in the real implementation, so concurrent callers each get a
        /// disjoint batch instead of blocking or double-claiming. Lives on this abstraction
        /// (rather than being raw SQL called directly from Infrastructure) because the SQL is a
        /// Persistence-layer/relational-provider concern.
        /// </summary>
        Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default);

        /// <summary>Same claim mechanism as ClaimOutboxMessagesAsync, for inbound webhook
        /// payloads awaiting processing — see InboxDispatcherBackgroundService.</summary>
        Task<List<InboxMessage>> ClaimInboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}
