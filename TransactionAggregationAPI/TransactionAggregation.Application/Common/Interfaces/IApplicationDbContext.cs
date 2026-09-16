
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

        Task<List<OutboxMessage>> ClaimOutboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default);

        Task<List<InboxMessage>> ClaimInboxMessagesAsync(int batchSize, CancellationToken cancellationToken = default);
    }
}