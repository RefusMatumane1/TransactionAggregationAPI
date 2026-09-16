namespace TransactionAggregation.Application.Abstractions.Authentication
{
    /// <summary>
    /// Provisions users directly in Keycloak via its Admin REST API, using the confidential
    /// "transaction-admin" service-account client (client-credentials grant, manage-users role).
    /// The app never stores or verifies passwords itself — Keycloak is the sole credential store.
    /// </summary>
    public interface IKeycloakAdminClient
    {
        /// <summary>Creates a new user in Keycloak and returns Keycloak's own user id — used
        /// as-is for the local CustomerId, so identity has exactly one source of truth. Throws
        /// KeycloakUserConflictException if the email is already taken.</summary>
        Task<Guid> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken = default);

        /// <summary>Looks up an existing user's id by email. Used by SeedData to make demo-user
        /// provisioning idempotent when the local database is reset but the Keycloak realm isn't.</summary>
        Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}
