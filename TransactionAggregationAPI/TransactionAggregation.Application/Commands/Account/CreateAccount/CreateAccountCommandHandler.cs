using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using SharedKernel.Common.ValueObjects;
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

            var customerExists = await _context.Customers
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
                return Result.Failure<Guid>(Error.NotFound("Customer", request.CustomerId));

            var duplicateExists = await _context.Accounts
                .AnyAsync(a => a.CustomerId == customerId && a.AccountNumber == request.AccountNumber, cancellationToken);

            if (duplicateExists)
                return Result.Failure<Guid>(Error.Validation(
                    $"Account with number '{request.AccountNumber}' already exists for this customer"));

            Domain.Entities.Account account;
            try
            {
                account = Domain.Entities.Account.Create(
                    customerId,
                    request.AccountNumber,
                    request.AccountName,
                    request.AccountType,
                    request.Currency);
            }
            catch (DomainException ex)
            {
                return Result.Failure<Guid>(Error.Validation(ex.Message));
            }

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Account {AccountNumber} created for customer {CustomerId}",
                request.AccountNumber, request.CustomerId);

            return Result.Success(account.Id.Value);
        }
    }
}