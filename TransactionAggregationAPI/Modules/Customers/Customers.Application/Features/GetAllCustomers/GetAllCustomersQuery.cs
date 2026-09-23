using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;
using Modules.Customers.Application.Common.Models;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetAllCustomers
{
    public sealed record GetAllCustomersQuery(
        int Page = 1,
        int PageSize = 20,
        string? SearchTerm = null) : IQuery<PagedResult<CustomerDto>>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(2);
    }
}
