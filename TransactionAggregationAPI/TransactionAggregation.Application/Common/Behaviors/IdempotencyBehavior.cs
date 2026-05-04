using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace TransactionAggregation.Application.Common.Behaviors
{
    public class IdempotencyBehavior<TRequest, TResponse>(IDistributedCache _cache,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : class
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IIdempotentRequest idempotentRequest)
                return await next(cancellationToken);

            var rawKey = idempotentRequest.IdempotencyKey;
            if (string.IsNullOrEmpty(rawKey))
                return await next(cancellationToken);

            var cacheKey = $"idempotent:{rawKey}";

            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedResult != null)
            {
                _logger.LogInformation(
                    "Returning cached result for idempotent request {IdempotencyKey}",
                    idempotentRequest.IdempotencyKey);

                return JsonSerializer.Deserialize<TResponse>(cachedResult)!;
            }

            var response = await next(cancellationToken);

            var serialized = JsonSerializer.Serialize(response);
            await _cache.SetStringAsync(
                cacheKey,
                serialized,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                },
                cancellationToken);

            return response;
        }
    }

    public interface IIdempotentRequest
    {
        string? IdempotencyKey { get; }
    }
}
