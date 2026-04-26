using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Transaction.GetTransaction
{
    public sealed record GetTransactionQuery(Guid TransactionId) : IQuery<TransactionDto>;
}
