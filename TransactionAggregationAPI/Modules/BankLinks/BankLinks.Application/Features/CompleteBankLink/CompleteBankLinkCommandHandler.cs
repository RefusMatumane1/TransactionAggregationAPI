using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using Modules.BankLinks.Application.Features.InitiateBankLink;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Application.Ports;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Application.Features.CompleteBankLink
{
    internal sealed class CompleteBankLinkCommandHandler(
        IBankLinksDbContext _context,
        IBankAggregatorClient _client,
        IBankLinkCredentialProtector _protector,
        IAccountProvisioningPort _accountProvisioning,
        IDistributedCache _cache,
        ILogger<CompleteBankLinkCommandHandler> logger)
        : ICommandHandler<CompleteBankLinkCommand, Guid>
    {
        public async Task<Result<Guid>> Handle(CompleteBankLinkCommand request, CancellationToken cancellationToken)
        {
            var cacheKey = InitiateBankLinkCommandHandler.StateCacheKey(request.State);
            var statePayloadJson = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (statePayloadJson is null)
                return Result.Failure<Guid>(Error.Validation("Authorization state is invalid or has expired. Please try linking again."));

            await _cache.RemoveAsync(cacheKey, cancellationToken);

            var statePayload = JsonSerializer.Deserialize<InitiateBankLinkCommandHandler.StatePayload>(statePayloadJson)!;
            var customerId = CustomerId.CreateFrom(statePayload.CustomerId);

            var link = await _context.BankLinks
                .FirstOrDefaultAsync(
                    b => b.CustomerId == customerId && b.Institution == statePayload.Institution,
                    cancellationToken);

            if (link is not { Status: BankLinkStatus.PendingAuthorization })
                return Result.Failure<Guid>(Error.Validation("No pending bank link found for this authorization."));

            var tokens = await _client.ExchangeAuthorizationCodeAsync(request.Code, cancellationToken);
            var linkedAccount = await _client.GetLinkedAccountAsync(tokens.AccessToken, cancellationToken);

            var provisionResult = await _accountProvisioning.ProvisionOrGetAccountAsync(
                statePayload.CustomerId,
                linkedAccount.AccountNumber,
                linkedAccount.AccountName,
                linkedAccount.AccountType,
                linkedAccount.Currency,
                cancellationToken);

            if (provisionResult.IsFailure)
                return Result.Failure<Guid>(provisionResult.Error);

            var accountId = provisionResult.Value;

            link.Activate(
                AccountId.CreateFrom(accountId),
                linkedAccount.ExternalAccountId,
                _protector.Protect(tokens.AccessToken),
                _protector.Protect(tokens.RefreshToken),
                tokens.ExpiresAtUtc);

            await _context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Bank link completed for customer {CustomerId}, institution {Institution}, account {AccountId}",
                statePayload.CustomerId, statePayload.Institution, accountId);

            return Result.Success(accountId);
        }
    }
}
