using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Account.GetCustomerAccounts
{
    internal sealed class GetCustomerAccountsQueryHandler(
        IApplicationDbContext _context,
        ILogger<GetCustomerAccountsQueryHandler> logger)
        : IQueryHandler<GetCustomerAccountsQuery, IEnumerable<AccountDto>>
    {
        public async Task<Result<IEnumerable<AccountDto>>> Handle(
            GetCustomerAccountsQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var customerExists = await _context.Customers
                    .AnyAsync(c => c.Id == customerId, cancellationToken);

                if (!customerExists)
                    return Result.Failure<IEnumerable<AccountDto>>(
                        Error.NotFound("Customer", request.CustomerId));

                var accounts = await _context.Accounts
                    .Where(a => a.CustomerId == customerId)
                    .ToListAsync(cancellationToken);

                var dtos = accounts.Select(a => new AccountDto(
                    a.Id.Value,
                    a.CustomerId.Value,
                    a.AccountNumber,
                    a.AccountName,
                    a.AccountType,
                    a.Balance,
                    a.Currency,
                    a.IsActive,
                    a.CreatedAt,
                    a.UpdatedAt));

                return Result.Success(dtos.AsEnumerable());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving accounts for customer {CustomerId}", request.CustomerId);
                return Result.Failure<IEnumerable<AccountDto>>(Error.Unexpected);
            }
        }
    }
}
