using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.Customer.CreateCustomer
{
    internal sealed class CreateCustomerCommandHandler(IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ILogger<CreateCustomerCommandHandler> logger) 
        : ICommandHandler<CreateCustomerCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var emailExists = await context.Customers
              .AnyAsync(c => c.Email == request.Email, cancellationToken);

                if (emailExists)
                    return Result.Failure<Guid>(Error.Conflict("Customer with this email already exists"));

                var customerId = CustomerId.Create();
                var passwordHash = passwordHasher.Hash(request.Password);
                Domain.Entities.Customer customer = Domain.Entities.Customer.Create(customerId, request.Email, request.Name, passwordHash);

                await context.Customers.AddAsync(customer, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Customer created with ID: {CustomerId}", customer.Id);

                return Result.Success<Guid>(customer.Id.Value);
            }
            catch (Exception)
            {
                logger.LogError("An error occurred while creating a customer with email: {Email}", request.Email);
                return Result.Failure<Guid>(Error.Unexpected);
            }
        }
    }
}
