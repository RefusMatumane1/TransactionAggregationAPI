using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using System.Linq.Expressions;

namespace Modules.Transactions.Domain.Services
{
    public static class LedgerEntries
    {
        // Booked rows are the ledger. Rows left Pending or Expired by the lifecycle that preceded the
        // insert-only ledger are kept unchanged but never read as transactions.
        public static readonly Expression<Func<Transaction, bool>> IsEntry =
            t => t.Status == TransactionStatus.Booked;
    }
}