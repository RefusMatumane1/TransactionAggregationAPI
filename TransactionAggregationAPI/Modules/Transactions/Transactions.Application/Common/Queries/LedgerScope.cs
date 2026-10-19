using BuildingBlocks.Application.Querying;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Services;

namespace Modules.Transactions.Application.Common.Queries
{
    internal static class LedgerScope
    {
        /// <summary>The ledger entries <paramref name="filter"/> admits: booked transactions at banks the caller may read.</summary>
        public static IQueryable<Transaction> Ledger(this IQueryable<Transaction> transactions, AccountFilter filter) =>
            transactions
                .AsNoTracking()
                .Where(LedgerEntries.IsEntry)
                .InScope(filter, t => t.Source.Name, t => t.ExternalAccountId);
    }
}
