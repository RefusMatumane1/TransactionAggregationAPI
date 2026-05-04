using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    public sealed record GetAllCustomersQuery(
        int Page = 1,
        int PageSize = 20,
        string? SearchTerm = null) : IQuery<PagedResult<CustomerDto>>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(2);
    }
}
