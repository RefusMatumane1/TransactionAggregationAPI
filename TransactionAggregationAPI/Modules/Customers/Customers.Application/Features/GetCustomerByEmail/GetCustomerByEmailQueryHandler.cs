using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;

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
            logger.LogInformation("Handling {RequestName} for email: {Email}", nameof(GetCustomerByEmailQuery), request.Email);
            var customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Email == request.Email, cancellationToken);

            if (customer is null)
                return Result.Failure<CustomerDto>(
                    Error.NotFound("Customer", request.Email));

            var dto = new CustomerDto(
                customer.Id.Value,
                customer.Email,
                customer.Name,
                customer.CreatedAt,
                customer.UpdatedAt);

            logger.LogInformation("Successfully retrieved customer with email: {Email}", request.Email);
            return Result.Success(dto);
        }
    }
}
