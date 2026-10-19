using Modules.Transactions.Contracts;
using Modules.Transactions.Domain.Entities;
using System.Linq.Expressions;

namespace Modules.Transactions.Domain.Services
{
    public static class LedgerEntries
    {
        public static readonly Expression<Func<Transaction, bool>> IsEntry =
            t => t.Status == TransactionStatus.Booked;
    }
}