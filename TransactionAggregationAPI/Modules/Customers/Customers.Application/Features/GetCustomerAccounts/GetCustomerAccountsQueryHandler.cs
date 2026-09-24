using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Contracts;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Features.GetCustomerAccounts
{
    internal sealed class GetCustomerAccountsQueryHandler(
        ICustomersDbContext _context,
        IAccountBalanceProvider _balanceProvider,
        ILogger<GetCustomerAccountsQueryHandler> logger)
        : IQueryHandler<GetCustomerAccountsQuery, IEnumerable<AccountDto>>
    {
        public async Task<Result<IEnumerable<AccountDto>>> Handle(
            GetCustomerAccountsQuery request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetCustomerAccountsQuery for CustomerId: {CustomerId}", request.CustomerId);

            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customerExists = await _context.Customers
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
            {
                logger.LogWarning("Customer {CustomerId} not found", request.CustomerId);
                return Result.Failure<IEnumerable<AccountDto>>(
                    CustomerErrors.NotFound(request.CustomerId));
            }

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .ToListAsync(cancellationToken);

            // Balances are derived from each account's transactions, which the Transactions module owns.
            var balancesByAccountId = await _balanceProvider.GetBalancesByCustomerAsync(customerId.Value, cancellationToken);

            var dtos = accounts.Select(a =>
            {
                var balance = balancesByAccountId.GetValueOrDefault(a.Id.Value, AccountBalance.Zero);
                return new AccountDto(
                    a.Id.Value,
                    a.CustomerId.Value,
                    a.AccountNumber,
                    a.AccountName,
                    a.AccountType,
                    balance.Booked,
                    a.Currency,
                    a.IsActive,
                    a.CreatedAt,
                    a.UpdatedAt,
                    balance.Pending,
                    balance.Available);
            });

            return Result.Success(dtos.AsEnumerable());
        }
    }
}