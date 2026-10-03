using System.Globalization;
using TransactionAggregationUI.Models.Audit;

namespace TransactionAggregationUI.Services
{
    public class AuditService(ApiClient api)
    {
        public async Task<(AuditEventPageModel? page, string? error)> SearchAsync(AuditFilter filter)
        {
            var query = ApiQuery.Of(
                ("cursor", filter.Cursor),
                ("includeTotal", filter.Cursor is null ? true : null),
                ("pageSize", filter.PageSize),
                ("channel", filter.Channel),
                ("eventType", filter.EventType),
                ("sourceName", filter.SourceName),
                ("externalAccountId", filter.ExternalAccountId),
                ("inboxMessageId", filter.InboxMessageId),
                ("transactionId", filter.TransactionId),
                ("externalTransactionId", filter.ExternalTransactionId),
                ("from", ToUtcIso(filter.From)),
                ("to", ToUtcIso(filter.To)));

            return await api.GetAsync<AuditEventPageModel>($"api/v1/admin/audit/events?{query}");
        }

        public Task<(TransactionLineageModel? Value, string? Error)> GetLineageAsync(Guid transactionId) =>
            api.GetAsync<TransactionLineageModel>(
                $"api/v1/admin/audit/transactions/{transactionId}/lineage",
                notFoundMessage: "No ingestion record for this transaction. It may predate the audit trail (e.g. seed data), or the id is wrong.");

        private static string? ToUtcIso(DateTime? local) =>
            local is { } value
                ? DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : null;
    }
}