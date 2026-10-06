using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Common;
using Modules.Customers.Application.DTOs;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.WebhookSources.Contracts;
using SharedKernel.Common.Models;

namespace Modules.Customers.Application.Features.LinkAccount
{
    internal sealed class LinkAccountCommandHandler(
        ICustomersDbContext context,
        IWebhookSourceDirectory banks,
        IUserContext userContext,
        TimeProvider time,
        ILogger<LinkAccountCommandHandler> logger)
        : ICommandHandler<LinkAccountCommand, LinkAccountResult>
    {
        public async Task<Result<LinkAccountResult>> Handle(LinkAccountCommand request, CancellationToken cancellationToken)
        {
            var customer = await FindAsync(request.CustomerId, cancellationToken);
            if (customer is null)
                return Result.Failure<LinkAccountResult>(CustomerRules.NotFound(request.CustomerId));

            // Transactions carry the bank's code exactly as registered; a link spelled any other way
            // would silently match nothing.
            var institution = await banks.FindBankCodeAsync(request.Institution, cancellationToken);
            if (institution is null)
                return Result.Failure<LinkAccountResult>(Error.Validation($"institution '{request.Institution}' is not a registered bank."));

            if (!customer.Link(institution, request.ExternalAccountId, time.GetUtcNow().UtcDateTime))
                return Result.Success(new LinkAccountResult(CustomerDto.From(customer, InstitutionAccess.All), Linked: false));

            context.StageAudit([CustomerAudit.Of(AuditEventTypes.CustomerAccountLinked, customer, userContext.UserId,
                $"Account linked to customer {customer.Reference}", institution, request.ExternalAccountId)]);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request linked the same account first (primary key); the outcome is the same.
                context.DiscardPendingChanges();
                var current = await FindAsync(request.CustomerId, cancellationToken);
                if (current is null || !current.Accounts.Any(a => a.Institution == institution && a.ExternalAccountId == request.ExternalAccountId))
                    throw;
                return Result.Success(new LinkAccountResult(CustomerDto.From(current, InstitutionAccess.All), Linked: false));
            }

            logger.LogInformation("Account at {Institution} linked to customer {CustomerId} by admin {AdminId}",
                institution, customer.Id.Value, userContext.UserId);

            return Result.Success(new LinkAccountResult(CustomerDto.From(customer, InstitutionAccess.All), Linked: true));
        }

        private Task<Customer?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(id);
            return context.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        }
    }
}