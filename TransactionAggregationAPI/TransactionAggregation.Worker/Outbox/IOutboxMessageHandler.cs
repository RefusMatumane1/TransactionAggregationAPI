using BuildingBlocks.Application.Caching;
using BuildingBlocks.Messaging.Outbox;

namespace TransactionAggregation.Worker.Outbox
{
    // One handler per outbox message type. A message whose SchemaVersion is newer than the handler
    // reads is dead-lettered rather than misread; an unknown type is dead-lettered too.
    public interface IOutboxMessageHandler
    {
        string MessageType { get; }

        int HighestReadableSchemaVersion { get; }

        // Throws on failure; the dispatcher classifies the exception as transient or permanent.
        Task HandleAsync(OutboxMessage message, OutboxDispatchRun run, CancellationToken cancellationToken);
    }

    // State shared by the handlers within one dispatch cycle. Handlers run concurrently.
    public sealed class OutboxDispatchRun(ICacheService cache)
    {
        private readonly HashSet<string> _invalidatedScopes = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _gate = new(1, 1);

        // Every message claimed in this cycle was committed before the claim, so one version bump
        // per scope covers them all. A failed bump is not remembered: the next caller tries again.
        public async Task InvalidateCacheScopeOnceAsync(string scope, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_invalidatedScopes.Contains(scope))
                    return;

                await cache.InvalidateScopeAsync(scope, cancellationToken);
                _invalidatedScopes.Add(scope);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}