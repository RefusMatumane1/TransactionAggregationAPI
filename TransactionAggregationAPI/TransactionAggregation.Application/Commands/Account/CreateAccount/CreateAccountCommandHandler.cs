using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using SharedKernel.Exceptions;

namespace TransactionAggregation.Application.Commands.Account.CreateAccount
{
    internal sealed class CreateAccountCommandHandler(
        IApplicationDbContext _context,
        ILogger<CreateAccountCommandHandler> logger)
        : ICommandHandler<CreateAccountCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customer = await _context.Customers
                .Include(c => c.Accounts)
                .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

            if (customer is null)
                return Result.Failure<Guid>(Error.NotFound("Customer", request.CustomerId));

            try
            {
                var account = customer.AddAccount(
                    request.AccountNumber,
                    request.AccountName,
                    request.AccountType,
                    request.Currency);

                await _context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Account {AccountNumber} created for customer {CustomerId}",
                    request.AccountNumber, request.CustomerId);

                return Result.Success(account.Id.Value);
            }
            catch (DomainException ex)
            {
                return Result.Failure<Guid>(Error.Validation(ex.Message));
            }
        }
    }
}