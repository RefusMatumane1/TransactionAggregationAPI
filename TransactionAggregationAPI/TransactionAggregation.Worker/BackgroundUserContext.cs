using BuildingBlocks.Application.Abstractions.Authentication;

namespace TransactionAggregation.Worker
{
    // The worker shares module wiring with the API, which registers handlers that depend on a user.
    // Background processing acts on no one's behalf, so any attempt to read a user fails loudly.
    internal sealed class BackgroundUserContext : IUserContext
    {
        public Guid UserId => throw NoUser();

        public InstitutionAccess InstitutionAccess => throw NoUser();

        private static InvalidOperationException NoUser() =>
            new("The background worker has no signed-in user; user-initiated operations belong in the API.");
    }
}