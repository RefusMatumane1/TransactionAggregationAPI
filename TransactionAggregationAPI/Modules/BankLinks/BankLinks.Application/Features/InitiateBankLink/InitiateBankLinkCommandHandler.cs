using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Application.Ports;
using Modules.BankLinks.Domain;
using Modules.BankLinks.Domain.ValueObjects;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using SharedKernel.Common.ValueObjects;
using System.Security.Cryptography;
using System.Text.Json;

namespace Modules.BankLinks.Application.Features.InitiateBankLink
{
    internal sealed class InitiateBankLinkCommandHandler(
        IBankLinksDbContext _context,
        IBankAggregatorClient _client,
        IDistributedCache _cache,
        ILogger<InitiateBankLinkCommandHandler> logger)
        : ICommandHandler<InitiateBankLinkCommand, string>
    {
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        public async Task<Result<string>> Handle(InitiateBankLinkCommand request, CancellationToken cancellationToken)
        {
            var customerId = CustomerId.CreateFrom(request.CustomerId);

            var existingLink = await _context.BankLinks
                .FirstOrDefaultAsync(
                    b => b.CustomerId == customerId && b.Institution == request.Institution,
                    cancellationToken);

            if (existingLink is { Status: BankLinkStatus.Active })
                return Result.Failure<string>(Error.Conflict($"{request.Institution} is already linked."));

            if (existingLink is null)
            {
                existingLink = BankLink.Create(customerId, request.Institution);
                _context.BankLinks.Add(existingLink);
            }
            else if (existingLink.Status != BankLinkStatus.PendingAuthorization)
            {
                existingLink.ResetForReauthorization();
            }
            // Still PendingAuthorization: an earlier attempt was abandoned — consent denied, or
            // the tab closed. It must be restartable, or that bank could never be linked again.
            // Starting over just issues a fresh state; if the old consent is somehow still
            // completed, the first completion activates the link and the other finds nothing pending.

            var state = GenerateState();
            var statePayload = JsonSerializer.Serialize(new StatePayload(request.CustomerId, request.Institution));

            await _cache.SetStringAsync(
                StateCacheKey(state),
                statePayload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = StateTtl },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            var authorizationUrl = _client.BuildAuthorizationUrl(request.Institution, state);

            logger.LogInformation(
                "Bank link initiated for customer {CustomerId}, institution {Institution}",
                request.CustomerId, request.Institution);

            return Result.Success(authorizationUrl);
        }

        private static string GenerateState() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string StateCacheKey(string state) => $"banklink:state:{state}";

        public sealed record StatePayload(Guid CustomerId, Institution Institution);
    }
}