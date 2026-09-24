using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Features.UpdateCustomer
{
    internal sealed class UpdateCustomerCommandHandler(ICustomersDbContext _context,
        ICacheService _cacheService,
        ILogger<UpdateCustomerCommandHandler> logger)
        : ICommandHandler<UpdateCustomerCommand>
    {
        public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling UpdateCustomerCommand for Customer ID {CustomerId}", request.CustomerId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

            if (customer is null)
                return Result.Failure(CustomerErrors.NotFound(request.CustomerId));

            var emailExists = await _context.Customers
                .AnyAsync(c => c.Email == request.Email && c.Id != customerId, cancellationToken);

            if (emailExists)
                return Result.Failure(Error.Conflict("Email already in use by another customer"));

            customer.Update(request.Email, request.Name);
            await _context.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPatternAsync($"customer:{request.CustomerId}*", cancellationToken);

            logger.LogInformation("Customer with ID {CustomerId} updated successfully", customerId);

            return Result.Success();
        }
    }
}