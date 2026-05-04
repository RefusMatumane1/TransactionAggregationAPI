using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    internal sealed class GetCustomerQueryHandler(
        IApplicationDbContext _context, ILogger<GetCustomerQueryHandler> logger)
        : IQueryHandler<GetCustomerQuery, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Handling GetCustomerQuery for CustomerId: {CustomerId}", request.CustomerId);
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customer = await _context.Customers
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
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while handling GetCustomerQuery for CustomerId: {CustomerId}", request.CustomerId);
                return Result.Failure<CustomerDto>(Error.Failure("Customer.RetrievalFailed", "An unexpected error occurred while retrieving the customer"));
            }
        }
    }
}
