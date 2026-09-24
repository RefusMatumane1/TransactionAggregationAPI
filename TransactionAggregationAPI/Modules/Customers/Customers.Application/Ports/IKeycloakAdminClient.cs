namespace Modules.Customers.Application.Ports
{
    /// <summary>
    /// Customer registration provisions a Keycloak identity. This is the port for that;
    /// KeycloakAdminClient in Customers.Infrastructure implements it. It lives in
    /// Application.Ports, not Contracts, because only this module's own Infrastructure
    /// implements it and no other module consumes it.
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