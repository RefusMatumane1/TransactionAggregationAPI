using MediatR;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Application.Common.Behaviors
{
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : class
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

        public CachingBehavior(ICacheService cacheService, ILogger<CachingBehavior<TRequest, TResponse>> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Check if request is cacheable
            if (request is not ICacheableQuery cacheableQuery)
                return await next();

            var cacheKey = GenerateCacheKey(request);
            var cachedResponse = await _cacheService.GetAsync<TResponse>(cacheKey, cancellationToken);

            if (cachedResponse is not null)
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
                return cachedResponse;
            }

            _logger.LogInformation("Cache miss for key: {CacheKey}", cacheKey);
            var response = await next();

            await _cacheService.SetAsync(
                cacheKey,
                response,
                cacheableQuery.CacheExpiration,
                cancellationToken);

            return response;
        }

        private static string GenerateCacheKey(TRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            var base64 = Convert.ToBase64String(bytes);
            return $"{typeof(TRequest).Name}:{base64}";
        }
    }

    public interface ICacheableQuery
    {
        TimeSpan? CacheExpiration { get; }
    }
}
