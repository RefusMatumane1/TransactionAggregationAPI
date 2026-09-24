using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Entities;
using System.Net.Http.Json;

namespace Modules.Transactions.Infrastructure.Services
{
    public class NotificationService(
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<NotificationOptions> options) : INotificationService
    {
        private readonly NotificationOptions _options = options.Value;

        public Task SendTransactionNotificationAsync(
            Transaction transaction,
            NotificationType type,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation(
                "Notification for transaction {TransactionId}, Type: {Type}",
                transaction.Id.Value,
                type);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Duplicates are already dropped by the time this runs, and the webhook post never
        /// throws — a failed or unconfigured alert channel can't affect ingestion.
        /// </summary>
        public async Task SendDuplicateInboundAlertAsync(
            DuplicateInboundDetectedOutboxPayload duplicate,
            CancellationToken cancellationToken = default)
        {
            logger.LogWarning(
                "Duplicate inbound {Level} from {SourceName} for account {ExternalAccountId}: {DuplicateCount} transaction(s) dropped {DuplicateExternalIds} (inbox message {InboxMessageId})",
                duplicate.Level,
                duplicate.SourceName,
                duplicate.ExternalAccountId,
                duplicate.DuplicateExternalIds.Count,
                duplicate.DuplicateExternalIds,
                duplicate.InboxMessageId);

            if (string.IsNullOrWhiteSpace(_options.DuplicateAlertWebhookUrl))
                return;

            await PostWebhookAsync(_options.DuplicateAlertWebhookUrl, new
            {
                alert_type = "duplicate_inbound",
                level = duplicate.Level,
                source_name = duplicate.SourceName,
                external_account_id = duplicate.ExternalAccountId,
                customer_id = duplicate.CustomerId,
                inbox_message_id = duplicate.InboxMessageId,
                duplicate_external_ids = duplicate.DuplicateExternalIds,
                detected_at = duplicate.DetectedAt
            }, cancellationToken);
        }

        private async Task PostWebhookAsync(string webhookUrl, object payload, CancellationToken cancellationToken)
        {
            try
            {
                var response = await httpClientFactory.CreateClient()
                    .PostAsJsonAsync(webhookUrl, payload, cancellationToken);
                response.EnsureSuccessStatusCode();

                logger.LogDebug("Webhook sent to {WebhookUrl}", webhookUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send webhook to {WebhookUrl}", webhookUrl);
            }
        }
    }

    public class NotificationOptions
    {
        /// <summary>Empty disables the webhook; duplicates are still logged and counted.</summary>
        public string DuplicateAlertWebhookUrl { get; set; } = string.Empty;
    }
}