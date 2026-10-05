using BuildingBlocks.Application.Caching;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel.Common.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BuildingBlocks.Application.Behaviors
{
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : class
    {
        public static readonly TimeSpan MaxExpiration = TimeSpan.FromHours(1);

        private readonly ICacheService _cacheService;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
        private readonly CachingOptions _options;

        public CachingBehavior(
            ICacheService cacheService, ILogger<CachingBehavior<TRequest, TResponse>> logger, CachingOptions? options = null)
        {
            _cacheService = cacheService;
            _logger = logger;
            _options = options ?? new CachingOptions();
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not ICacheableQuery cacheableQuery)
                return await next();

            var version = await _cacheService.GetScopeVersionAsync(cacheableQuery.CacheScope, cancellationToken);
            if (version is null)
            {
                _logger.LogDebug("Cache unavailable for scope {CacheScope} — running {RequestName} uncached",
                    cacheableQuery.CacheScope, typeof(TRequest).Name);
                return await next();
            }

            var cacheKey = BuildCacheKey(request, cacheableQuery.CacheScope, version.Value);
            var cachedResponse = await _cacheService.GetAsync<TResponse>(cacheKey, cancellationToken);

            if (cachedResponse is not null)
            {
                _logger.LogDebug("Cache hit for {RequestName} in scope {CacheScope}", typeof(TRequest).Name, cacheableQuery.CacheScope);
                return cachedResponse;
            }

            var response = await next();

            if (response is Result { IsFailure: true })
                return response;

            var requested = _options.ScopeExpirationMinutes.TryGetValue(cacheableQuery.CacheScope, out var minutes) && minutes > 0
                ? TimeSpan.FromMinutes(minutes)
                : cacheableQuery.CacheExpiration;
            var expiration = requested is { } lifetime && lifetime < MaxExpiration ? lifetime : MaxExpiration;
            await _cacheService.SetAsync(cacheKey, response, expiration, cancellationToken);

            return response;
        }

        private static string BuildCacheKey(TRequest request, string scope, long version)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            return $"{scope}:v{version}:{typeof(TRequest).Name}:{hash}";
        }
    }
}