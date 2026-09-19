using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using TransactionAggregation.Application.Abstractions.Authentication;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.Customer.CreateCustomer
{
    internal sealed class CreateCustomerCommandHandler(IApplicationDbContext context,
        IKeycloakAdminClient keycloakAdminClient,
        ILogger<CreateCustomerCommandHandler> logger)
        : ICommandHandler<CreateCustomerCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            var emailExists = await context.Customers
                .AnyAsync(c => c.Email == request.Email, cancellationToken);

            if (emailExists)
                return Result.Failure<Guid>(Error.Conflict("Customer with this email already exists"));

            Guid keycloakUserId;
            try
            {
                keycloakUserId = await keycloakAdminClient.CreateUserAsync(
                    request.Email, request.Name, request.Password, cancellationToken);
            }
            catch (KeycloakUserConflictException)
            {
                return Result.Failure<Guid>(Error.Conflict("Customer with this email already exists"));
            }

            var customerId = CustomerId.CreateFrom(keycloakUserId);
            Domain.Entities.Customer customer = Domain.Entities.Customer.Create(customerId, request.Email, request.Name);

            await context.Customers.AddAsync(customer, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Customer created with ID: {CustomerId}", customer.Id);

            return Result.Success<Guid>(customer.Id.Value);
        }
    }
}