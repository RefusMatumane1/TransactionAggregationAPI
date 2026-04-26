using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface ITransactionSource
    {
        string SourceName { get; }
        Task<IReadOnlyList<ExternalTransactionDTO>> GetTransactionsAsync(
            CustomerId customerId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default);
    }
}
