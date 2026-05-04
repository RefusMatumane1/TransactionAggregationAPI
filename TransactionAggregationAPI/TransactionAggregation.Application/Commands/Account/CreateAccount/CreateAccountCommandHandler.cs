using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Exceptions;

namespace TransactionAggregation.Application.Commands.Account.CreateAccount
{
    internal sealed class CreateAccountCommandHandler(
        IApplicationDbContext _context,
        ILogger<CreateAccountCommandHandler> logger)
        : ICommandHandler<CreateAccountCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customer = await _context.Customers
                    .Include(c => c.Accounts)
                    .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

                if (customer is null)
                    return Result.Failure<Guid>(Error.NotFound("Customer", request.CustomerId));

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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating account for customer {CustomerId}", request.CustomerId);
                return Result.Failure<Guid>(Error.Unexpected);
            }
        }
    }
}
