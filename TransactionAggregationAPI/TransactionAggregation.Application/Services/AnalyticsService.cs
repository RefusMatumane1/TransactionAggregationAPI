using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services
{
    public class AnalyticsService(ILogger<AnalyticsService> _logger) : IAnalyticsService
    {
        public Task TrackTransactionCreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[Analytics] Transaction created: {TransactionId}, Customer: {CustomerId}, Amount: {Amount} {Currency}",
                transaction.Id.Value,
                transaction.CustomerId.Value,
                transaction.Amount.Amount,
                transaction.Amount.Currency);

            return TrackMetricAsync("transactions.created", 1, new Dictionary<string, string>
            {
                ["currency"] = transaction.Amount.Currency,
                ["category"] = transaction.Category.ToString(),
                ["is_income"] = transaction.IsIncome.ToString()
            }, cancellationToken);
        }

        public async Task TrackTransactionApprovedAsync(Transaction transaction, string approvedBy, CancellationToken cancellationToken = default)
        {
            var approvalTime = DateTime.UtcNow - transaction.CreatedAt;

            _logger.LogInformation(
                "[Analytics] Transaction approved: {TransactionId}, By: {ApprovedBy}, ApprovalTime: {ApprovalTimeMs}ms",
                transaction.Id.Value,
                approvedBy,
                approvalTime.TotalMilliseconds);

            await TrackMetricAsync("transactions.approved", 1, new Dictionary<string, string>
            {
                ["approved_by"] = approvedBy,
                ["category"] = transaction.Category.ToString()
            }, cancellationToken);

            await TrackPerformanceAsync("transaction.approval", approvalTime, true, cancellationToken);
        }

        public Task TrackTransactionRejectedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "[Analytics] Transaction rejected: {TransactionId}, Reason: {Reason}",
                transaction.Id.Value,
                reason);

            return TrackMetricAsync("transactions.rejected", 1, new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["category"] = transaction.Category.ToString()
            }, cancellationToken);
        }

        public Task TrackTransactionCategorizedAsync(
            Transaction transaction,
            TransactionCategory oldCategory,
            TransactionCategory newCategory,
            bool isAuto,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[Analytics] Transaction recategorized: {TransactionId}, {OldCategory} -> {NewCategory}, Auto: {IsAuto}",
                transaction.Id.Value,
                oldCategory,
                newCategory,
                isAuto);

            return TrackMetricAsync("transactions.categorized", 1, new Dictionary<string, string>
            {
                ["old_category"] = oldCategory.ToString(),
                ["new_category"] = newCategory.ToString(),
                ["is_auto"] = isAuto.ToString()
            }, cancellationToken);
        }

        public Task TrackAutoCategorizationAsync(
            Transaction transaction,
            TransactionCategory oldCategory,
            TransactionCategory newCategory,
            CancellationToken cancellationToken = default)
        {
            return TrackTransactionCategorizedAsync(transaction, oldCategory, newCategory, true, cancellationToken);
        }

        public Task TrackTransactionSyncedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            return TrackMetricAsync("transactions.synced", 1, new Dictionary<string, string>
            {
                ["source"] = transaction.Source.Name
            }, cancellationToken);
        }

        public Task TrackMetricAsync(
            string metricName,
            double value,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "[Metric] {MetricName}: {Value}, Tags: {@Tags}",
                metricName,
                value,
                tags);

            return Task.CompletedTask;
        }

        public Task TrackUserBehaviorAsync(
            Guid customerId,
            string action,
            Dictionary<string, object>? properties = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[UserBehavior] Customer: {CustomerId}, Action: {Action}, Properties: {@Properties}",
                customerId,
                action,
                properties);

            return Task.CompletedTask;
        }

        public Task TrackPerformanceAsync(
            string operationName,
            TimeSpan duration,
            bool isSuccess = true,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug(
                "[Performance] {Operation} completed in {DurationMs}ms, Success: {IsSuccess}",
                operationName,
                duration.TotalMilliseconds,
                isSuccess);

            return TrackMetricAsync($"performance.{operationName}.duration", duration.TotalMilliseconds, new Dictionary<string, string>
            {
                ["operation"] = operationName,
                ["success"] = isSuccess.ToString()
            }, cancellationToken);
        }

        public async Task TrackErrorAsync(
            Exception exception,
            string context,
            Dictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            var errorTags = new Dictionary<string, string>
            {
                ["context"] = context,
                ["exception_type"] = exception.GetType().Name,
                ["message"] = exception.Message
            };

            if (metadata != null)
            {
                foreach (var kvp in metadata)
                    errorTags[kvp.Key] = kvp.Value;
            }

            await TrackMetricAsync("errors.total", 1, errorTags, cancellationToken);

            _logger.LogError(exception, "[Error] Context: {Context}, Metadata: {@Metadata}", context, metadata);
        }
    }
}
