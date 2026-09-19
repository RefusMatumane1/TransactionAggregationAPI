using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using SharedKernel.Common.Models;
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
            logger.LogInformation("Handling GetCustomerAccountsQuery for CustomerId: {CustomerId}", request.CustomerId);

            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var customerExists = await _context.Customers
                .AnyAsync(c => c.Id == customerId, cancellationToken);

            if (!customerExists)
            {
                logger.LogWarning("Customer {CustomerId} not found", request.CustomerId);
                return Result.Failure<IEnumerable<AccountDto>>(
                    Error.NotFound("Customer", request.CustomerId));
            }

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .ToListAsync(cancellationToken);

            // Account.Balance is never maintained by any handler (Credit()/Debit() are
            // domain-tested but unwired) — always 0 if used directly. Balance is the sum
            // of each account's linked transactions instead, the same computed-not-stored
            // convention GetTransactionSummaryQueryHandler already uses for NetBalance.
            var balancesByAccountId = (await _context.Transactions
                .AsNoTracking()
                .Where(t => t.CustomerId == customerId && t.AccountId != null)
                .Select(t => new { AccountId = t.AccountId!.Value, t.Amount.Amount })
                .ToListAsync(cancellationToken))
                .GroupBy(t => t.AccountId)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var dtos = accounts.Select(a => new AccountDto(
                a.Id.Value,
                a.CustomerId.Value,
                a.AccountNumber,
                a.AccountName,
                a.AccountType,
                balancesByAccountId.GetValueOrDefault(a.Id.Value, 0m),
                a.Currency,
                a.IsActive,
                a.CreatedAt,
                a.UpdatedAt));

            return Result.Success(dtos.AsEnumerable());
        }
    }
}