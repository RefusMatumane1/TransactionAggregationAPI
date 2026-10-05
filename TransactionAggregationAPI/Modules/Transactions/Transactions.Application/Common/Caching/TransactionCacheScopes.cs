namespace Modules.Transactions.Application.Common.Caching
{
    public static class TransactionCacheScopes
    {
        // Reads of the ledger itself (lists, one transaction): any recorded transaction can change
        // them, so the outbox bumps this scope once per dispatch cycle that recorded something.
        public const string All = "transactions";

        // Reads of the daily read model. It changes only when the scheduled refresh commits, which
        // bumps this scope, so entries can live as long as the refresh interval.
        public const string Aggregates = "transaction-aggregates";

        // Default lifetime of an aggregate entry; Caching:ScopeExpirationMinutes overrides it per scope.
        public static readonly TimeSpan AggregatesExpiration = TimeSpan.FromMinutes(55);
    }
}