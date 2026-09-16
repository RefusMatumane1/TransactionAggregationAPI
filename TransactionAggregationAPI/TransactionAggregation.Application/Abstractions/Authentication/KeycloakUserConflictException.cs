namespace TransactionAggregation.Application.Abstractions.Authentication
{
    /// <summary>Thrown by IKeycloakAdminClient when Keycloak rejects user creation because the
    /// email/username is already taken, so CreateCustomerCommandHandler can map it to
    /// Error.Conflict the same way it would a local uniqueness violation.</summary>
    public sealed class KeycloakUserConflictException : Exception
    {
        public KeycloakUserConflictException(string message) : base(message) { }
    }
}
