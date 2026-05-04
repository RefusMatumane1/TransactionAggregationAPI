
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.Customer.DeleteCustomer
{
    internal sealed class DeleteCustomerCommandHandler(IApplicationDbContext _context,
        ILogger<DeleteCustomerCommandHandler> logger)
        : ICommandHandler<DeleteCustomerCommand>
    {

        public async Task<Result> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Handling DeleteCustomerCommand for CustomerId: {CustomerId}", request.CustomerId);
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customer = await _context.Customers
                    .Include(c => c.Transactions)
                    .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

                if (customer is null)
                    return Result.Failure(Error.NotFound("Customer", request.CustomerId));

                if (customer.Transactions.Any())
                    return Result.Failure(Error.Validation("Cannot delete customer with existing transactions"));

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Successfully deleted Customer with Id: {CustomerId}", request.CustomerId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while handling DeleteCustomerCommand for CustomerId: {CustomerId}", request.CustomerId);
                return Result.Failure(Error.Failure("UnexpectedError", "An unexpected error occurred while deleting the customer"));
            }
        }
    }
}
