using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Features.CreateCustomer
{
    internal sealed class CreateCustomerCommandHandler(
        ICustomersDbContext context,
        IUserContext userContext,
        ILogger<CreateCustomerCommandHandler> logger)
        : ICommandHandler<CreateCustomerCommand, CustomerDto>
    {
        public async Task<Result<CustomerDto>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
        {
            if (await ReferenceTakenAsync(request.Reference, cancellationToken))
                return Conflict(request.Reference);

            var customer = Customer.Create(request.Reference, request.Name);
            context.Customers.Add(customer);
            context.StageAudit([CustomerAudit.Of(AuditEventTypes.CustomerCreated, customer, userContext.UserId,
                $"Customer {customer.Reference} created")]);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                context.DiscardPendingChanges();
                if (!await ReferenceTakenAsync(request.Reference, cancellationToken))
                    throw;
                return Conflict(request.Reference);
            }

            logger.LogInformation("Customer {CustomerId} ({CustomerReference}) created by admin {AdminId}",
                customer.Id.Value, customer.Reference, userContext.UserId);

            return Result.Success(CustomerDto.From(customer, InstitutionAccess.All));
        }

        private Task<bool> ReferenceTakenAsync(string reference, CancellationToken cancellationToken) =>
            context.Customers.AsNoTracking().AnyAsync(c => c.Reference == reference, cancellationToken);

        private static Result<CustomerDto> Conflict(string reference) =>
            Result.Failure<CustomerDto>(Error.Conflict($"A customer with reference '{reference}' already exists."));
    }
}