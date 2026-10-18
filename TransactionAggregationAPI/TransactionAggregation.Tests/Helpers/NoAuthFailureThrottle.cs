using TransactionAggregationAPI.RateLimiting;

namespace TransactionAggregation.Tests.Helpers
{
    // The real throttle needs Redis; API tests run without it.
    internal sealed class NoAuthFailureThrottle : IAuthFailureThrottle
    {
        public Task<TimeSpan?> BlockedForAsync(string client, CancellationToken cancellationToken) => Task.FromResult<TimeSpan?>(null);

        public Task RecordFailureAsync(string client, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
