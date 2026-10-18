namespace Modules.Transactions.Application.Common.Caching
{
    public static class TransactionCacheScopes
    {
        public const string All = "transactions";

        public const string Aggregates = "transaction-aggregates";

        public static readonly TimeSpan AggregatesExpiration = TimeSpan.FromMinutes(55);
    }
}