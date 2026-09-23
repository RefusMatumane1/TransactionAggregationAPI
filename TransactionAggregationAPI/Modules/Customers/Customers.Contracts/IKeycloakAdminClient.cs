namespace Modules.Customers.Contracts
{
    /// <summary>
    /// Owned by Customers: customer creation needs to provision a Keycloak identity, but
    /// the Keycloak admin HTTP client is a cross-cutting infrastructure concern that
    /// hasn't been pulled into this module (it's registered alongside the rest of
    /// Modules.Transactions.Infrastructure's auth/cache/notification adapters). The
    /// implementation (KeycloakAdminClient, in Modules.Transactions.Infrastructure,
    /// wired in Program.cs) is what actually talks to Keycloak — Customers itself has no
    /// dependency on that HTTP client or its options.
    /// </summary>
    public interface IKeycloakAdminClient
    {
        Task<Guid> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken = default);

        Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    }

    public sealed class KeycloakUserConflictException : Exception
    {
        public KeycloakUserConflictException(string message) : base(message) { }
    }
}
