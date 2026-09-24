namespace Modules.WebhookSources.Contracts
{
    /// <summary>
    /// Published contract for checking a source by name, for channels that identify the
    /// sender some other way than an API key — the Kafka consumer reads the name from a
    /// record header. The WebhookSources registry is the single list of inbound sources, so
    /// deactivating a source there stops it on every channel.
    /// </summary>
    public interface IWebhookSourceDirectory
    {
        /// <summary>True when a source with exactly this name exists and is active.</summary>
        Task<bool> IsActiveAsync(string sourceName, CancellationToken cancellationToken = default);

        /// <summary>
        /// The institutions an active source may deliver transactions for (compared
        /// case-insensitively). Empty when the source is unknown, inactive or not yet scoped —
        /// callers must treat empty as "authorized for nothing".
        /// </summary>
        Task<IReadOnlySet<string>> GetAuthorizedInstitutionsAsync(string sourceName, CancellationToken cancellationToken = default);
    }
}