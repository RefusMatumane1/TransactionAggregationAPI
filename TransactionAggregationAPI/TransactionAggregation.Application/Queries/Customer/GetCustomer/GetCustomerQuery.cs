using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Behaviors;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    public sealed record GetCustomerQuery(Guid CustomerId) : IQuery<CustomerDto>, ICacheableQuery, ICacheKeyPrefix
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public string CachePrefix => $"customer:{CustomerId}";
    }
}
