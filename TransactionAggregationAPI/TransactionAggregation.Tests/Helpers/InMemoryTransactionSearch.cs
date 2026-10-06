using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;

namespace TransactionAggregation.Tests.Helpers
{
    public sealed class InMemoryTransactionSearch : ITransactionSearch
    {
        public IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term) =>
            transactions.Where(t => t.Description.ToLower().Contains(term.ToLower()));
    }
}