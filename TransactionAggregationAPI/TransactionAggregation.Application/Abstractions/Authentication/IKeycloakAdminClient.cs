namespace TransactionAggregation.Application.Abstractions.Authentication
{
    public interface IKeycloakAdminClient
    {
        Task<Guid> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken = default);

        Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}