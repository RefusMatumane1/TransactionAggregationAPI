
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionsDbContext
    {
        DbSet<Transaction> Transactions { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Forgets every change a failed SaveChangesAsync left behind — this context's tracked
        /// entities and any Outbox rows still pending on the shared MessagingDbContext (added
        /// by the caller or by domain-event handlers during that save) — so a retry, or the
        /// next unit of work in the same scope, doesn't re-submit them.
        /// </summary>
        void DiscardPendingChanges();
    }
}