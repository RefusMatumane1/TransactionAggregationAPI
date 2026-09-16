using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.BankLink.InitiateBankLink
{
    internal sealed class InitiateBankLinkCommandHandler(
        IApplicationDbContext _context,
        IBankAggregatorClient _client,
        IDistributedCache _cache,
        ILogger<InitiateBankLinkCommandHandler> logger)
        : ICommandHandler<InitiateBankLinkCommand, string>
    {
        // One-time-use CSRF token for the OAuth callback; short-lived since the consent flow
        // is a single interactive browser round-trip.
        private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(10);

        public async Task<Result<string>> Handle(InitiateBankLinkCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var customerId = CustomerId.CreateFrom(request.CustomerId);

                var existingLink = await _context.BankLinks
                    .FirstOrDefaultAsync(
                        b => b.CustomerId == customerId && b.Institution == request.Institution,
                        cancellationToken);

                if (existingLink is { Status: BankLinkStatus.Active or BankLinkStatus.PendingAuthorization })
                    return Result.Failure<string>(Error.Conflict(
                        $"{request.Institution} is already linked (or a link is already in progress)."));

                if (existingLink is null)
                {
                    existingLink = Domain.Entities.BankLink.Create(customerId, request.Institution);
                    _context.BankLinks.Add(existingLink);
                }
                else
                {
                    existingLink.ResetForReauthorization();
                }

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
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error initiating bank link for customer {CustomerId}, institution {Institution}",
                    request.CustomerId, request.Institution);
                return Result.Failure<string>(Error.Unexpected);
            }
        }

        private static string GenerateState() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static string StateCacheKey(string state) => $"banklink:state:{state}";

        public sealed record StatePayload(Guid CustomerId, Institution Institution);
    }
}
