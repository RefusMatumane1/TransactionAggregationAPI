using BuildingBlocks.Application.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Persistence;
using Modules.Notifications.Contracts;

namespace Modules.Notifications.Application.Contracts
{
    // Duplicates are per bank, so are their destinations: the bank's own URL, else the configured default, else only the log.
    // A URL embeds its credential: it is never logged, only which kind of destination was used.
    internal sealed class DuplicateAlertNotifier(
        INotificationsDbContext context,
        IAlertUrlProtector protector,
        IAlertWebhookPoster poster,
        IOptions<NotificationOptions> options,
        ILogger<DuplicateAlertNotifier> logger) : IDuplicateAlertNotifier
    {
        public async Task NotifyAsync(DuplicateAlert alert, CancellationToken cancellationToken = default)
        {
            var protectedUrl = await context.AlertWebhooks
                .AsNoTracking()
                .Where(w => w.BankCode == alert.SourceName)
                .Select(w => w.ProtectedUrl)
                .FirstOrDefaultAsync(cancellationToken);

            var bankUrl = protectedUrl is null ? null : protector.Unprotect(protectedUrl);
            var url = !string.IsNullOrWhiteSpace(bankUrl) ? bankUrl : options.Value.DuplicateAlertWebhookUrl;
            var destination = !string.IsNullOrWhiteSpace(bankUrl) ? "bank" : !string.IsNullOrWhiteSpace(url) ? "default" : "none";

            logger.LogWarning(
                "Duplicate inbound {Level} from {SourceName} for account {AccountRef}: {DuplicateCount} transaction(s) not recorded again (inbox message {InboxMessageId}); alert destination: {Destination}",
                alert.Level,
                alert.SourceName,
                LogRedaction.Account(alert.ExternalAccountId),
                alert.DuplicateExternalIds.Count,
                alert.InboxMessageId,
                destination);

            if (string.IsNullOrWhiteSpace(url))
                return;

            await poster.PostAsync(url, new
            {
                alert_type = "duplicate_inbound",
                level = alert.Level,
                source_name = alert.SourceName,
                external_account_id = alert.ExternalAccountId,
                inbox_message_id = alert.InboxMessageId,
                duplicate_external_ids = alert.DuplicateExternalIds,
                detected_at = alert.DetectedAt
            }, cancellationToken);
        }
    }
}