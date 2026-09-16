namespace TransactionAggregation.Infrastructure.Providers
{
    /// <summary>
    /// Configuration for the account-data aggregator integration. South Africa has no
    /// mandated open-banking regime with public per-bank retail APIs, so this targets a
    /// licensed aggregator (e.g. Stitch — stitch.money — which already holds agreements with
    /// FNB, Standard Bank, Absa, Nedbank and Capitec) rather than each bank directly.
    ///
    /// The OAuth2 authorization-code/refresh-token endpoints below follow RFC 6749's standard
    /// shape, which is safe to assume for any spec-compliant provider. AuthorizeEndpoint and
    /// AccountEndpoint, and their response shapes in HttpBankAggregatorClient, are
    /// provider-specific — confirm the exact paths and JSON/GraphQL schema against your chosen
    /// aggregator's current developer docs before go-live; do not assume the ones sketched here
    /// are correct without checking. Transaction data itself is not pulled from the aggregator
    /// at all — it's pushed to WebhookEndpoints, authenticated against the WebhookSource table
    /// (managed live via WebhookSourceEndpoints/the admin UI), not anything in this options
    /// class.
    /// </summary>
    public sealed class BankAggregatorOptions
    {
        public const string SectionName = "BankAggregator";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Where the customer's browser is redirected to grant consent.</summary>
        public string AuthorizeEndpoint { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string AccountEndpoint { get; set; } = string.Empty;

        /// <summary>Must exactly match a redirect URI registered with the aggregator.</summary>
        public string RedirectUri { get; set; } = string.Empty;
    }
}
