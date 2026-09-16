namespace TransactionAggregation.Infrastructure.Authentication
{
    /// <summary>
    /// Keycloak connection settings. Authority is the server root the API itself uses to reach
    /// Keycloak (the in-cluster/compose-network service address — reliable, no dependency on
    /// host DNS/hosts-file tricks), which can differ from PublicIssuer, the externally-reachable
    /// realm URL the browser uses and which therefore appears as the `iss` claim on every token.
    /// JwtBearer is configured to fetch signing keys from Authority but validate tokens' issuer
    /// against PublicIssuer — see Program.cs.
    /// </summary>
    public sealed class KeycloakOptions
    {
        public const string SectionName = "Keycloak";

        /// <summary>Server root only, e.g. "http://keycloak:8080" — no /realms/{realm} suffix.</summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>Full external issuer string, e.g. "http://localhost:8081/realms/transaction-aggregation".
        /// Must exactly match the `iss` claim Keycloak stamps onto tokens.</summary>
        public string PublicIssuer { get; set; } = string.Empty;

        public string Realm { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;

        /// <summary>Confidential client with service accounts enabled and the realm-management
        /// "manage-users"/"view-users" roles — used only for the client-credentials grant behind
        /// KeycloakAdminClient, never exposed to the browser.</summary>
        public string AdminClientId { get; set; } = string.Empty;
        public string AdminClientSecret { get; set; } = string.Empty;
    }
}
