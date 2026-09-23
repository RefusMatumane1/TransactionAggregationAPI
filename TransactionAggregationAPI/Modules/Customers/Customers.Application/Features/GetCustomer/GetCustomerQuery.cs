using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetCustomer
{
    public sealed record GetCustomerQuery(Guid CustomerId) : IQuery<CustomerDto>, ICacheableQuery, ICacheKeyPrefix
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public string CachePrefix => $"customer:{CustomerId}";
    }
}
