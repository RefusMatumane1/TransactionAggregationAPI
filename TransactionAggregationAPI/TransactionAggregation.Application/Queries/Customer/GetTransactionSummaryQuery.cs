using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Customer
{
    public sealed record GetTransactionSummaryQuery(
        Guid CustomerId,
        DateTime StartDate,
        DateTime EndDate) : IQuery<TransactionSummaryDto>;
}
