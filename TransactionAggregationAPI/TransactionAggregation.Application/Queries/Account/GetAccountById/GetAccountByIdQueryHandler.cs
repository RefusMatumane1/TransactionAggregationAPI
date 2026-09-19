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
            logger.LogInformation("Handling GetAccountByIdQuery for AccountId: {AccountId}", request.AccountId);

            var accountId = AccountId.CreateFrom(request.AccountId);

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

            if (account is null)
            {
                logger.LogWarning("Account {AccountId} not found", request.AccountId);
                return Result.Failure<AccountDto>(
                    Error.NotFound("Account", request.AccountId));
            }

            // Account.Balance is never maintained by any handler (Credit()/Debit() are
            // domain-tested but unwired) — always 0 if used directly. Balance is the sum
            // of linked transactions instead, the same computed-not-stored convention
            // GetTransactionSummaryQueryHandler already uses for NetBalance, so it's
            // always consistent with Transactions (the actual source of truth) with no
            // stored-counter concurrency/lost-update risk.
            var balance = await _context.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == accountId)
                .SumAsync(t => t.Amount.Amount, cancellationToken);

            var dto = new AccountDto(
                account.Id.Value,
                account.CustomerId.Value,
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                balance,
                account.Currency,
                account.IsActive,
                account.CreatedAt,
                account.UpdatedAt);

            return Result.Success(dto);
        }
    }
}