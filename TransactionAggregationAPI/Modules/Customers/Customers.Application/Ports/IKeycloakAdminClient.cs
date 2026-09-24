namespace Modules.Customers.Application.Ports
{
    /// <summary>Provisions the Keycloak identity for a newly registered customer.</summary>
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