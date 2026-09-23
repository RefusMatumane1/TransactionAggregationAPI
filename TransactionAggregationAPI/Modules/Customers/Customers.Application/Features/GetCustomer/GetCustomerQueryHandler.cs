using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;

namespace Modules.Customers.Application.Features.GetCustomer
{
    internal sealed class GetCustomerQueryHandler(
        ICustomersDbContext _context, ILogger<GetCustomerQueryHandler> logger)
        : IQueryHandler<GetCustomerQuery, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetCustomerQuery for CustomerId: {CustomerId}", request.CustomerId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

            if (customer is null)
                return Result.Failure<CustomerDto>(Error.NotFound("Customer", request.CustomerId));

            var dto = new CustomerDto(
                customer.Id.Value,
                customer.Email,
                customer.Name,
                customer.CreatedAt,
                customer.UpdatedAt);

            logger.LogInformation("Successfully retrieved Customer with Id: {CustomerId}", customer.Id.Value);
            return Result.Success(dto);
        }
    }
}
