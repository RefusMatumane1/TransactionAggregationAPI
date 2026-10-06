using BuildingBlocks.Application.Logging;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Outbox;

namespace TransactionAggregation.Worker.Notifications
{
    // The URL embeds the hook's credential: it comes from secrets and is never logged.
    public sealed class NotificationService(
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<NotificationOptions> options) : INotificationService
    {
        public const string HttpClientName = "duplicate-alerts";

        private readonly NotificationOptions _options = options.Value;

        public async Task SendDuplicateInboundAlertAsync(
            DuplicateInboundDetectedOutboxPayload duplicate,
            CancellationToken cancellationToken = default)
        {
            logger.LogWarning(
                "Duplicate inbound {Level} from {SourceName} for account {AccountRef}: {DuplicateCount} transaction(s) not recorded again (inbox message {InboxMessageId})",
                duplicate.Level,
                duplicate.SourceName,
                LogRedaction.Account(duplicate.ExternalAccountId),
                duplicate.DuplicateExternalIds.Count,
                duplicate.InboxMessageId);

            if (string.IsNullOrWhiteSpace(_options.DuplicateAlertWebhookUrl))
                return;

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.DuplicateAlertWebhookUrl)
            {
                Content = JsonContent.Create(new
                {
                    alert_type = "duplicate_inbound",
                    level = duplicate.Level,
                    source_name = duplicate.SourceName,
                    external_account_id = duplicate.ExternalAccountId,
                    inbox_message_id = duplicate.InboxMessageId,
                    duplicate_external_ids = duplicate.DuplicateExternalIds,
                    detected_at = duplicate.DetectedAt
                })
            };
            OutboundUrlRedaction.Redact(request);

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

            // 4xx other than 408/429 is permanent; the rest is transient.
            response.EnsureSuccessStatusCode();
        }
    }

    public sealed class NotificationOptions
    {
        public const string SectionName = "NotificationOptions";

        public string DuplicateAlertWebhookUrl { get; set; } = string.Empty;
    }
}