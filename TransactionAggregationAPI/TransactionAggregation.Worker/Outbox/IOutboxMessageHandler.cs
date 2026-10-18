using BuildingBlocks.Application.Caching;
using BuildingBlocks.Messaging.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace TransactionAggregation.Worker.Outbox
{
    public interface IOutboxMessageHandler
    {
        string MessageType { get; }

        int HighestReadableSchemaVersion { get; }

        Task HandleAsync(OutboxMessage message, OutboxDispatchRun run, CancellationToken cancellationToken);
    }

    public sealed class OutboxDispatchRun(ICacheService cache, ILogger? logger = null)
    {
        private readonly HashSet<string> _attemptedScopes = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        public async Task InvalidateCacheScopeOnceAsync(string scope, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (!_attemptedScopes.Add(scope))
                    return;

                await cache.InvalidateScopeAsync(scope, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Cache scope {CacheScope} could not be invalidated; publishing anyway, cached reads expire on their own", scope);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}