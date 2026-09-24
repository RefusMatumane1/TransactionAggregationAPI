using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Features.GetCustomerByEmail
{
    internal sealed class GetCustomerByEmailQueryHandler(ICustomersDbContext _context,
        ILogger<GetCustomerByEmailQueryHandler> logger)
        : IQueryHandler<GetCustomerByEmailQuery, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(
            GetCustomerByEmailQuery request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Looking up customer {CustomerId} by email", request.CustomerId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == request.Email && c.Id == customerId, cancellationToken);

            if (customer is null)
                return Result.Failure<CustomerDto>(
                    CustomerErrors.NotFoundByEmail());

            var dto = new CustomerDto(
                customer.Id.Value,
                customer.Email,
                customer.Name,
                customer.CreatedAt,
                customer.UpdatedAt);

            logger.LogInformation("Retrieved customer {CustomerId} by email", customer.Id.Value);
            return Result.Success(dto);
        }
    }
}