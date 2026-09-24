using SharedKernel.Abstractions.Authentication;

namespace TransactionAggregation.Worker
{
    /// <summary>
    /// The worker has no signed-in user. Registered only so the module registrations it shares
    /// with the API resolve; a handler that reads UserId from here was sent from the wrong host,
    /// and fails loudly rather than attributing the change to an empty Guid.
    /// </summary>
    internal sealed class BackgroundUserContext : IUserContext
    {
        public Guid UserId => throw new InvalidOperationException(
            "There is no user context in the background worker — user-initiated commands belong in the API.");
    }
}