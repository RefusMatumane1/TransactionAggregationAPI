using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Queries.Customer
{
    public sealed record GetCustomerTransactionsQuery(
        Guid CustomerId,
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        TransactionCategory? Category = null,
        int Page = 1,
        int PageSize = 20) : IQuery<PagedResult<TransactionDto>>;
}
