using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    // Free-text matching is store-specific (PostgreSQL: case-insensitive LIKE served by a trigram
    // index), so the query handler depends on this port rather than on a provider function.
    public interface ITransactionSearch
    {
        // Rows whose description contains the term, case-insensitively. The term is matched
        // literally: '%' and '_' in it are not wildcards.
        IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term);
    }
}