using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Application.Common.Interfaces
{

    public interface ITransactionSearch
    {
        IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term);
    }
}