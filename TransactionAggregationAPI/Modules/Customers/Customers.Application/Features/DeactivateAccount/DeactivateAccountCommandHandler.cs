using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Customers.Application.Errors;
using Modules.Customers.Application.Persistence;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;

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
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.CustomerId == customerId, cancellationToken);

            if (account is null)
                return Result.Failure(AccountErrors.NotFound(request.AccountId));

            account.Deactivate();
            await _context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Account {AccountId} deactivated", request.AccountId);
            return Result.Success();
        }
    }
}