using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using System.Linq.Expressions;

namespace Modules.Transactions.Domain.Services
{
    public static class LedgerEntries
    {
        // Booked rows are the ledger; legacy Pending/Expired rows are kept but never read.
        public static readonly Expression<Func<Transaction, bool>> IsEntry =
            t => t.Status == TransactionStatus.Booked;
    }
}