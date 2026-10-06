namespace Modules.Transactions.Application.Common.Caching
{
    public static class TransactionCacheScopes
    {
        // Ledger reads: the outbox bumps this scope per dispatch cycle that recorded something.
        public const string All = "transactions";

        // Daily read model: changes only when the refresh commits, which bumps this scope.
        public const string Aggregates = "transaction-aggregates";

        public static readonly TimeSpan AggregatesExpiration = TimeSpan.FromMinutes(55);
    }
}