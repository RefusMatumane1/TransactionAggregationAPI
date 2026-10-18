namespace Modules.Notifications.Contracts
{
    public sealed record DuplicateAlert(
        string Level,
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<string> DuplicateExternalIds,
        Guid? InboxMessageId,
        DateTime DetectedAt);

    public interface IDuplicateAlertNotifier
    {
        Task NotifyAsync(DuplicateAlert alert, CancellationToken cancellationToken = default);
    }
}