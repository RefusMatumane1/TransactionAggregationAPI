using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using SharedKernel.Common.Interfaces;
using SharedKernel.Common.ValueObjects;
using System.Text;
using System.Text.Json;

namespace Modules.Transactions.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly NotificationOptions _options;

        public NotificationService(
            ILogger<NotificationService> logger,
            IHttpClientFactory httpClientFactory,
            IOptions<NotificationOptions> options)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task SendTransactionNotificationAsync(
            Transaction transaction,
            NotificationType type,
            CancellationToken cancellationToken = default)
        {
            var message = type switch
            {
                NotificationType.TransactionCreated => $"New transaction: {transaction.Description} for {transaction.Amount.Formatted}",
                NotificationType.TransactionApproved => $"Transaction approved: {transaction.Description}",
                NotificationType.TransactionRejected => $"Transaction rejected: {transaction.Description}",
                NotificationType.TransactionFlagged => $"Transaction flagged for review: {transaction.Description}",
                NotificationType.TransactionSettled => $"Transaction settled: {transaction.Description}",
                NotificationType.TransactionRefunded => $"Transaction refunded: {transaction.Description}",
                _ => $"Transaction update: {transaction.Description}"
            };

            await SendEmailAsync(
                GetCustomerEmail(transaction.CustomerId),
                $"Transaction {type}",
                message,
                cancellationToken);

            if (_options.EnablePushNotifications)
            {
                await SendPushNotificationAsync(
                    GetCustomerDeviceToken(transaction.CustomerId),
                    message,
                    cancellationToken);
            }

            _logger.LogInformation(
                "Notification sent for transaction {TransactionId}, Type: {Type}",
                transaction.Id.Value,
                type);
        }

        public async Task SendHighValueTransactionAlertAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            var alertMessage = $"HIGH VALUE ALERT: {transaction.Amount.Formatted} transaction at {transaction.Description}";

            await SendEmailAsync(
                _options.FraudTeamEmail,
                "High Value Transaction Alert",
                alertMessage,
                cancellationToken);

            if (!string.IsNullOrEmpty(_options.SmsEnabled))
            {
                await SendSmsAsync(
                    GetCustomerPhone(transaction.CustomerId),
                    alertMessage,
                    cancellationToken);
            }

            _logger.LogWarning(
                "High value alert sent for transaction {TransactionId}, Amount: {Amount}",
                transaction.Id.Value,
                transaction.Amount.Formatted);
        }

        public async Task SendFraudAlertAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        {
            await SendWebhookAsync(_options.FraudWebhookUrl, new
            {
                alert_type = "fraud",
                transaction_id = transaction.Id.Value,
                customer_id = transaction.CustomerId.Value,
                reason = reason,
                amount = transaction.Amount.Amount,
                currency = transaction.Amount.Currency,
                timestamp = DateTime.UtcNow
            }, cancellationToken);

            _logger.LogError(
                "Fraud alert triggered for transaction {TransactionId}, Reason: {Reason}",
                transaction.Id.Value,
                reason);
        }

        public async Task SendTransactionSummaryAsync(
            Guid customerId,
            DailySummary summary,
            CancellationToken cancellationToken = default)
        {
            var emailBody = $@"
            <h2>Daily Transaction Summary - {summary.Date:D}</h2>
            <p>Total Transactions: {summary.TransactionCount}</p>
            <p>Total Spent: {summary.TotalSpent:C}</p>
            <p>Total Income: {summary.TotalIncome:C}</p>
            <h3>Category Breakdown:</h3>
            <ul>
                {string.Join("", summary.CategoryBreakdown.Select(c => $"<li>{c.Key}: {c.Value:C}</li>"))}
            </ul>
        ";

            await SendEmailAsync(
                GetCustomerEmail(CustomerId.CreateFrom(customerId)),
                "Your Daily Transaction Summary",
                emailBody,
                cancellationToken,
                isHtml: true);
        }

        public async Task SendWebhookAsync(string webhookUrl, object payload, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(webhookUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogDebug("Webhook sent to {WebhookUrl}", webhookUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook to {WebhookUrl}", webhookUrl);
            }
        }

        /// <summary>
        /// Duplicates are already dropped by the time this runs, and SendWebhookAsync never
        /// throws — a failed or unconfigured alert channel can't affect ingestion.
        /// </summary>
        public async Task SendDuplicateInboundAlertAsync(
            DuplicateInboundDetectedOutboxPayload duplicate,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "Duplicate inbound {Level} from {SourceName} for account {ExternalAccountId}: {DuplicateCount} transaction(s) dropped {DuplicateExternalIds} (inbox message {InboxMessageId})",
                duplicate.Level,
                duplicate.SourceName,
                duplicate.ExternalAccountId,
                duplicate.DuplicateExternalIds.Count,
                duplicate.DuplicateExternalIds,
                duplicate.InboxMessageId);

            if (!string.IsNullOrWhiteSpace(_options.DuplicateAlertWebhookUrl))
            {
                await SendWebhookAsync(_options.DuplicateAlertWebhookUrl, new
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
        }

        private async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct, bool isHtml = false)
        {
            await Task.CompletedTask;
        }

        private async Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct)
        {
            await Task.CompletedTask;
        }

        private async Task SendPushNotificationAsync(string deviceToken, string message, CancellationToken ct)
        {
            await Task.CompletedTask;
        }

        private string GetCustomerEmail(CustomerId customerId) => $"{customerId.Value}@example.com";
        private string GetCustomerPhone(CustomerId customerId) => "+1234567890";
        private string GetCustomerDeviceToken(CustomerId customerId) => "device_token_123";
    }

    public class NotificationOptions
    {
        public string FraudTeamEmail { get; set; } = "fraud@example.com";
        public string FraudWebhookUrl { get; set; } = "https://webhook.site/fraud-alerts";
        public bool EnablePushNotifications { get; set; } = true;
        public string SmsEnabled { get; set; } = "true";

        /// <summary>Empty disables the webhook; duplicates are still logged and counted.</summary>
        public string DuplicateAlertWebhookUrl { get; set; } = string.Empty;
    }
}