using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    public interface ITransactionsDbContext
    {
        DbSet<Transaction> Transactions { get; }

        DbSet<DailyTotal> DailyTotals { get; }

        DbSet<AggregationCheckpoint> AggregationCheckpoints { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Staged events are written to the audit trail inside the next SaveChangesAsync's database
        // transaction, so they commit (or roll back) with the changes they describe.
        void StageAudit(IEnumerable<AuditEventRecord> events);

        void DiscardPendingChanges();
    }
}