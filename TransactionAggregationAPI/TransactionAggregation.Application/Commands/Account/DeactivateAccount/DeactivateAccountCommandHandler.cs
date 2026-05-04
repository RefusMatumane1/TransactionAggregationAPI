using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.Account.DeactivateAccount
{
    internal sealed class DeactivateAccountCommandHandler(
        IApplicationDbContext _context,
        ILogger<DeactivateAccountCommandHandler> logger)
        : ICommandHandler<DeactivateAccountCommand>
    {
        public async Task<Result> Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var accountId = AccountId.CreateFrom(request.AccountId);

                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

                if (account is null)
                    return Result.Failure(Error.NotFound("Account", request.AccountId));

                account.Deactivate();
                await _context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Account {AccountId} deactivated", request.AccountId);
                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deactivating account {AccountId}", request.AccountId);
                return Result.Failure(Error.Unexpected);
            }
        }
    }
}
