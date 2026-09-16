namespace TransactionAggregation.Infrastructure.Providers
{
    /// <summary>Thrown when the aggregator rejects a token as expired/revoked (HTTP 401) during
    /// the OAuth consent/linking handshake — distinguishes "needs a refresh/re-consent" from a
    /// transient failure that Polly should just retry.</summary>
    public sealed class BankAggregatorUnauthorizedException : Exception
    {
        public BankAggregatorUnauthorizedException(string message) : base(message) { }
    }
}
