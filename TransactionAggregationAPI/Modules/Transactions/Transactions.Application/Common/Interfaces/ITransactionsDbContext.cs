using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionsDbContext
    {
        DbSet<Transaction> Transactions { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        void StageAudit(IEnumerable<AuditEventRecord> events);

        void DiscardPendingChanges();
    }
}