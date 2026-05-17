using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface ITransactionCategorizationService
    {
        Task<TransactionCategory> CategorizeTransactionAsync(Transaction transaction,
            CancellationToken cancellationToken);
    }
}
