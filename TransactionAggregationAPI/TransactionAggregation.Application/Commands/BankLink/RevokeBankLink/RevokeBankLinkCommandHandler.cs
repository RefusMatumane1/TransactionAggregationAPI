using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Commands.BankLink.RevokeBankLink
{
    internal sealed class RevokeBankLinkCommandHandler(
        IApplicationDbContext _context,
        ILogger<RevokeBankLinkCommandHandler> logger)
        : ICommandHandler<RevokeBankLinkCommand>
    {
        public async Task<Result> Handle(RevokeBankLinkCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);
                var bankLinkId = Domain.Common.ValueObjects.BankLinkId.CreateFrom(request.BankLinkId);

                var link = await _context.BankLinks
                    .FirstOrDefaultAsync(b => b.Id == bankLinkId, cancellationToken);

                // Ownership check on the loaded entity, not just the path — see AccountEndpoints
                // for the class of bug this pattern avoids.
                if (link is null || link.CustomerId != customerId)
                    return Result.Failure(Error.NotFound("BankLink", request.BankLinkId));

                link.Revoke();
                await _context.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Bank link {BankLinkId} revoked for customer {CustomerId}",
                    request.BankLinkId, request.CustomerId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error revoking bank link {BankLinkId}", request.BankLinkId);
                return Result.Failure(Error.Unexpected);
            }
        }
    }
}
