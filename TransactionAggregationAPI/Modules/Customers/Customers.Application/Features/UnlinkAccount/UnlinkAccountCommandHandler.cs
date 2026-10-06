using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Features.UnlinkAccount
{
    internal sealed class UnlinkAccountCommandHandler(
        ICustomersDbContext context,
        IUserContext userContext,
        ILogger<UnlinkAccountCommandHandler> logger)
        : ICommandHandler<UnlinkAccountCommand>
    {
        public async Task<Result> Handle(UnlinkAccountCommand request, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);
            var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
            if (customer is null)
                return Result.Failure(CustomerRules.NotFound(request.CustomerId));

            var account = customer.Accounts.FirstOrDefault(a =>
                string.Equals(a.Institution, request.Institution, StringComparison.OrdinalIgnoreCase)
                && a.ExternalAccountId == request.ExternalAccountId);
            if (account is null || !customer.Unlink(account.Institution, account.ExternalAccountId))
                return Result.Failure(Error.NotFound("Linked account", $"{request.Institution}/{request.ExternalAccountId}"));

            context.StageAudit([CustomerAudit.Of(AuditEventTypes.CustomerAccountUnlinked, customer, userContext.UserId,
                $"Account unlinked from customer {customer.Reference}", account.Institution, account.ExternalAccountId)]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Account at {Institution} unlinked from customer {CustomerId} by admin {AdminId}",
                account.Institution, customer.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}