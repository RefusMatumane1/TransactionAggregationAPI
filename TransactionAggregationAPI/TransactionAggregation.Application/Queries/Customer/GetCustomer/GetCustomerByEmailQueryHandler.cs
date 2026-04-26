using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    internal sealed class GetCustomerByEmailQueryHandler(IApplicationDbContext _context,
        ILogger<GetCustomerByEmailQueryHandler> logger)
        : IQueryHandler<GetCustomerByEmailQuery, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(
            GetCustomerByEmailQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
             logger.LogInformation("Handling {RequestName} for email: {Email}", nameof(GetCustomerByEmailQuery), request.Email);
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email == request.Email, cancellationToken);

                if (customer is null)
                    return Result.Failure<CustomerDto>(
                        Error.NotFound("Customer.NotFound", $"Customer with email {request.Email} not found"));

                var dto = new CustomerDto(
                    customer.Id.Value,
                    customer.Email,
                    customer.Name,
                    customer.CreatedAt,
                    customer.UpdatedAt);

                logger.LogInformation("Successfully retrieved customer with email: {Email}", request.Email);
                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while handling {RequestName} for email: {Email}", nameof(GetCustomerByEmailQuery), request.Email);
                return Result.Failure<CustomerDto>(
                    Error.Failure("Customer.RetrievalError", $"An error occurred while retrieving customer with email {request.Email}"));
            }
        }
    }
}
