namespace Modules.Notifications.Contracts
{
    // A transaction or delivery that a bank sent more than once. Duplicates are per bank, and so are the alerts.
    public sealed record DuplicateAlert(
        string Level,
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<string> DuplicateExternalIds,
        Guid? InboxMessageId,
        DateTime DetectedAt);

    public interface IDuplicateAlertNotifier
    {
        // Sends the alert to the bank's own destination, else the configured default, else only logs it.
        // Throws when a destination was reached and refused, or could not be reached: the caller retries or dead-letters.
        Task NotifyAsync(DuplicateAlert alert, CancellationToken cancellationToken = default);
    }
}