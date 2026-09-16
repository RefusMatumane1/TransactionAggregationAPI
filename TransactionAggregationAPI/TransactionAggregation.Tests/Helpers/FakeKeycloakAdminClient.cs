using System.Collections.Concurrent;
using TransactionAggregation.Application.Abstractions.Authentication;

namespace TransactionAggregation.Tests.Helpers;

/// <summary>
/// In-memory stand-in for Keycloak's Admin API, used wherever a real IKeycloakAdminClient would
/// otherwise require a live Keycloak (integration tests). Mimics the one behavior callers
/// actually depend on: rejecting a second user with the same email.
/// </summary>
public sealed class FakeKeycloakAdminClient : IKeycloakAdminClient
{
    private readonly ConcurrentDictionary<string, Guid> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);

    public Task<Guid> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        if (!_usersByEmail.TryAdd(email, id))
            throw new KeycloakUserConflictException($"A Keycloak user with email '{email}' already exists.");

        return Task.FromResult(id);
    }

    public Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_usersByEmail.TryGetValue(email, out var id) ? (Guid?)id : null);
}
