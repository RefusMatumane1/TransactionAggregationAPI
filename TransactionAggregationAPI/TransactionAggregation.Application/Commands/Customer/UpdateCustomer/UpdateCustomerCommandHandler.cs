
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.Customer.UpdateCustomer
{
    internal sealed class UpdateCustomerCommandHandler(IApplicationDbContext _context,
        ILogger<UpdateCustomerCommandHandler> logger)
        : ICommandHandler<UpdateCustomerCommand>
    {
        public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Handling UpdateCustomerCommand for Customer ID {CustomerId}", request.CustomerId);
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

                if (customer is null)
                    return Result.Failure(Error.NotFound("Customer.NotFound", "Customer not found"));

                var emailExists = await _context.Customers
                    .AnyAsync(c => c.Email == request.Email && c.Id != customerId, cancellationToken);

                if (emailExists)
                    return Result.Failure(Error.Conflict("Email already in use by another customer"));

                customer.Update(request.Email, request.Name);
                await _context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Customer with ID {CustomerId} updated successfully", customerId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while updating customer with ID {CustomerId}", request.CustomerId);
                return Result.Failure(Error.Failure("500","An error occurred while updating the customer"));
            }
        }
    }
}
