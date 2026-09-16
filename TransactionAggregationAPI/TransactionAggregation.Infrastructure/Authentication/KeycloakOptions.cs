namespace TransactionAggregation.Infrastructure.Authentication
{
    public sealed class KeycloakOptions
    {
        public const string SectionName = "Keycloak";

        public string Authority { get; set; } = string.Empty;

        public string PublicIssuer { get; set; } = string.Empty;

        public string Realm { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        public string AdminClientId { get; set; } = string.Empty;
        public string AdminClientSecret { get; set; } = string.Empty;
    }
}