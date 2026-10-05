using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;

namespace TransactionAggregation.Tests.Helpers
{
    // The in-memory store has no ILIKE; the Postgres strategy (and its escaping) is covered by
    // the Postgres suite.
    public sealed class InMemoryTransactionSearch : ITransactionSearch
    {
        public IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term) =>
            transactions.Where(t => t.Description.ToLower().Contains(term.ToLower()));
    }
}