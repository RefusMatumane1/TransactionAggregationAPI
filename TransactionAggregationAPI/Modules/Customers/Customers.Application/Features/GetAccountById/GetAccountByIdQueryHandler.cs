using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Contracts;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

namespace Modules.Customers.Application.Features.GetAccountById
{
    internal sealed class GetAccountByIdQueryHandler(
        ICustomersDbContext _context,
        IAccountBalanceProvider _balanceProvider,
        ILogger<GetAccountByIdQueryHandler> logger)
        : IQueryHandler<GetAccountByIdQuery, AccountDto>
    {
        public async Task<Result<AccountDto>> Handle(
            GetAccountByIdQuery request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Handling GetAccountByIdQuery for AccountId: {AccountId}", request.AccountId);

            var accountId = AccountId.CreateFrom(request.AccountId);
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId && a.CustomerId == customerId, cancellationToken);

            if (account is null)
            {
                logger.LogWarning("Account {AccountId} not found", request.AccountId);
                return Result.Failure<AccountDto>(
                    AccountErrors.NotFound(request.AccountId));
            }

            // Account.Balance is never maintained by any handler (Credit()/Debit() are
            // domain-tested but unwired) — always 0 if used directly. Balance is the sum
            // of linked transactions instead, resolved through IAccountBalanceProvider
            // since Transactions lives in a different module/schema now.
            var balance = await _balanceProvider.GetBalanceAsync(accountId.Value, cancellationToken);

            var dto = new AccountDto(
                account.Id.Value,
                account.CustomerId.Value,
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                balance.Booked,
                account.Currency,
                account.IsActive,
                account.CreatedAt,
                account.UpdatedAt,
                balance.Pending,
                balance.Available);

            return Result.Success(dto);
        }
    }
}