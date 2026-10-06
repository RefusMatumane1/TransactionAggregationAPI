using BuildingBlocks.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Features.GetCustomer
{
    internal sealed class GetCustomerQueryHandler(ICustomersDbContext context) : IQueryHandler<GetCustomerQuery, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
        {
            var id = CustomerId.CreateFrom(request.CustomerId);
            var customer = await context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            // Not visible is reported exactly like not found.
            return customer is not null && request.Access.CanSee(customer)
                ? Result.Success(CustomerDto.From(customer, request.Access))
                : Result.Failure<CustomerDto>(CustomerRules.NotFound(request.CustomerId));
        }
    }
}