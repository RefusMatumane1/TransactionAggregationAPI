using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Domain.Services
{
    public interface ITransactionProcessingService
    {
        Task<Transaction> ProcessTransactionAsync(
            CustomerId customerId,
            Money amount,
            string description,
            TransactionSource source,
            DateTime date,
            CancellationToken cancellationToken = default);

        Task<Transaction> AutoCategorizeTransactionAsync(
            Transaction transaction,
            CancellationToken cancellationToken = default);

        Task<bool> ValidateTransactionAsync(
            Transaction transaction,
            CancellationToken cancellationToken = default);
    }
}
