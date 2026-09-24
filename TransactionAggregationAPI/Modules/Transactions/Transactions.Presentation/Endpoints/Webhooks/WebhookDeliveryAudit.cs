using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;

namespace Modules.Transactions.Presentation.Endpoints.Webhooks
{
    /// <summary>
    /// Channel metadata for webhook deliveries, and direct audit writes for the webhook
    /// outcomes that never reach the inbox (bad key, failed validation).
    /// </summary>
    internal static class WebhookDeliveryAudit
    {
        public const string UnauthenticatedSource = "unauthenticated";

        public static InboundDelivery Describe(HttpContext httpContext, bool idempotencyKeyProvided)
        {
            var metadata = BaseMetadata(httpContext);
            metadata["idempotencyKeyProvided"] = idempotencyKeyProvided ? "true" : "false";
            return new InboundDelivery(AuditChannels.Webhook, metadata);
        }

        /// <summary>
        /// Best effort: the caller is already getting a 4xx, and turning that into a 500
        /// because the audit write failed would only make a legitimate sender retry.
        /// The failure is logged at Error so it can be alerted on.
        /// </summary>
        public static async Task TryRecordAsync(
            IAuditTrail auditTrail,
            ILogger logger,
            HttpContext httpContext,
            string eventType,
            string sourceName,
            string detail,
            string? externalAccountId = null,
            IReadOnlyDictionary<string, string>? extraMetadata = null)
        {
            var metadata = BaseMetadata(httpContext);
            if (extraMetadata is not null)
            {
                foreach (var (key, value) in extraMetadata)
                    metadata[key] = value;
            }

            try
            {
                await auditTrail.RecordAsync(
                [
                    new AuditEventRecord(
                        EventId: Guid.NewGuid(),
                        EventType: eventType,
                        OccurredAt: DateTime.UtcNow,
                        Channel: AuditChannels.Webhook,
                        SourceName: sourceName,
                        ExternalAccountId: externalAccountId,
                        Detail: detail,
                        Metadata: metadata,
                        TraceId: InboundAudit.CurrentTraceId)
                ], httpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to record audit event {EventType} for webhook source {SourceName}", eventType, sourceName);
            }
        }

        private static Dictionary<string, string> BaseMetadata(HttpContext httpContext)
        {
            // RemoteIpAddress is the real client once UseForwardedHeaders has run (Program.cs).
            var metadata = new Dictionary<string, string>
            {
                ["httpMethod"] = httpContext.Request.Method,
                ["path"] = httpContext.Request.Path.Value ?? string.Empty,
                ["requestId"] = httpContext.TraceIdentifier
            };

            if (httpContext.Connection.RemoteIpAddress is { } remoteIp)
                metadata["remoteIp"] = remoteIp.ToString();

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(userAgent))
                metadata["userAgent"] = userAgent.Length <= 256 ? userAgent : userAgent[..256];

            return metadata;
        }
    }
}