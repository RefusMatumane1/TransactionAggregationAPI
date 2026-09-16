namespace TransactionAggregation.Infrastructure.Providers
{
    public sealed class BankAggregatorOptions
    {
        public const string SectionName = "BankAggregator";

        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;

        public string AuthorizeEndpoint { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string AccountEndpoint { get; set; } = string.Empty;

        public string RedirectUri { get; set; } = string.Empty;
    }
}