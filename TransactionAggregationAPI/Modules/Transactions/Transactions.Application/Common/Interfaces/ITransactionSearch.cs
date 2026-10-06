using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{
    // Store-specific (PostgreSQL: ILIKE served by a trigram index), hence a port.
    public interface ITransactionSearch
    {
        // The term is matched literally: '%' and '_' are not wildcards.
        IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term);
    }
}