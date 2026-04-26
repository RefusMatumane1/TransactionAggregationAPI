using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface ITransactionAggregator
    {
        Task<IReadOnlyList<Transaction>> AggregateCustomerTransactionsAsync(
            Guid customerId,
            DateTime? FromDate,
            DateTime? ToDate,
            CancellationToken cancellationToken = default);
    }
}
