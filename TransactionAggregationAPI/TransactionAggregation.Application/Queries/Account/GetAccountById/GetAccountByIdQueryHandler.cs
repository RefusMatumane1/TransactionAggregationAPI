using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Queries.Account.GetAccountById
{
    internal sealed class GetAccountByIdQueryHandler(
        IApplicationDbContext _context,
        ILogger<GetAccountByIdQueryHandler> logger)
        : IQueryHandler<GetAccountByIdQuery, AccountDto>
    {
        public async Task<Result<AccountDto>> Handle(
            GetAccountByIdQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                var accountId = AccountId.CreateFrom(request.AccountId);

                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

                if (account is null)
                    return Result.Failure<AccountDto>(
                        Error.NotFound("Account", request.AccountId));

                var dto = new AccountDto(
                    account.Id.Value,
                    account.CustomerId.Value,
                    account.AccountNumber,
                    account.AccountName,
                    account.AccountType,
                    account.Balance,
                    account.Currency,
                    account.IsActive,
                    account.CreatedAt,
                    account.UpdatedAt);

                return Result.Success(dto);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving account {AccountId}", request.AccountId);
                return Result.Failure<AccountDto>(Error.Unexpected);
            }
        }
    }
}
