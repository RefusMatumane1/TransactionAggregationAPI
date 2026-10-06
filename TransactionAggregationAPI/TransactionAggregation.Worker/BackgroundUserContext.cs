using BuildingBlocks.Application.Abstractions.Authentication;

namespace TransactionAggregation.Worker
{
    // Background processing acts for no one, so reading a user fails loudly.
    internal sealed class BackgroundUserContext : IUserContext
    {
        public Guid UserId => throw NoUser();

        public InstitutionAccess InstitutionAccess => throw NoUser();

        private static InvalidOperationException NoUser() =>
            new("The background worker has no signed-in user; user-initiated operations belong in the API.");
    }
}