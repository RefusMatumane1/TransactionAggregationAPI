using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using Modules.Customers.Application.Persistence;

namespace Modules.Customers.Application.Features.DeactivateAccount
{
    internal sealed class DeactivateAccountCommandHandler(
        ICustomersDbContext _context,
        ILogger<DeactivateAccountCommandHandler> logger)
        : ICommandHandler<DeactivateAccountCommand>
    {
        public async Task<Result> Handle(DeactivateAccountCommand request, CancellationToken cancellationToken)
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
    }
}
