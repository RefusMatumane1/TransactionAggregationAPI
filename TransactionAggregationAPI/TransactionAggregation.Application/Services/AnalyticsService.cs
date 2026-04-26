using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services
{
    public class AnalyticsService(ILogger<AnalyticsService> _logger,
            IApplicationDbContext _context) : IAnalyticsService
    {

        public async Task TrackTransactionCreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "[Analytics] Transaction created: {TransactionId}, Customer: {CustomerId}, Amount: {Amount} {Currency}",
                transaction.Id.Value,
                transaction.CustomerId.Value,
                transaction.Amount.Amount,
                transaction.Amount.Currency);

            // Track metric
            await TrackMetricAsync("transactions.created", 1, new Dictionary<string, string>
            {
                ["currency"] = transaction.Amount.Currency,
                ["category"] = transaction.Category.ToString(),
                ["is_income"] = transaction.IsIncome.ToString()
            }, cancellationToken);

            // Store analytics event for later processing
            //var analyticsEvent = new AnalyticsEvent
            //{
            //    Id = Guid.NewGuid(),
            //    EventType = "TransactionCreated",
            //    CustomerId = transaction.CustomerId.Value,
            //    TransactionId = transaction.Id.Value,
            //    Timestamp = DateTime.UtcNow,
            //    Data = JsonSerializer.Serialize(new
            //    {
            //        transaction.Amount.Amount,
            //        transaction.Amount.Currency,
            //        transaction.Category,
            //        transaction.Source.Name
            //    })
            //};

            //await _context.AnalyticsEvents.AddAsync(analyticsEvent, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
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

        public async Task TrackTransactionRejectedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning(
                "[Analytics] Transaction rejected: {TransactionId}, Reason: {Reason}",
                transaction.Id.Value,
                reason);

            await TrackMetricAsync("transactions.rejected", 1, new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["category"] = transaction.Category.ToString()
            }, cancellationToken);
        }

        public async Task TrackTransactionCategorizedAsync(
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

            await TrackMetricAsync("transactions.categorized", 1, new Dictionary<string, string>
            {
                ["old_category"] = oldCategory.ToString(),
                ["new_category"] = newCategory.ToString(),
                ["is_auto"] = isAuto.ToString()
            }, cancellationToken);
        }

        public async Task TrackAutoCategorizationAsync(
            Transaction transaction,
            TransactionCategory oldCategory,
            TransactionCategory newCategory,
            CancellationToken cancellationToken = default)
        {
            await TrackTransactionCategorizedAsync(transaction, oldCategory, newCategory, true, cancellationToken);
        }

        public async Task TrackTransactionSyncedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            await TrackMetricAsync("transactions.synced", 1, new Dictionary<string, string>
            {
                ["source"] = transaction.Source.Name
            }, cancellationToken);
        }

        public async Task TrackMetricAsync(
            string metricName,
            double value,
            Dictionary<string, string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            // Send to metrics collector (Prometheus, Datadog, etc.)
           // _metricsCollector.RecordMetric(metricName, value, tags);

            // Store in database for historical analysis
            //var metric = new MetricRecord
            //{
            //    Id = Guid.NewGuid(),
            //    Name = metricName,
            //    Value = value,
            //    Tags = tags ?? new(),
            //    Timestamp = DateTime.UtcNow
            //};

            //await _context.Metrics.AddAsync(metric, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task TrackUserBehaviorAsync(
            Guid customerId,
            string action,
            Dictionary<string, object>? properties = null,
            CancellationToken cancellationToken = default)
        {
            //var behavior = new UserBehaviorEvent
            //{
            //    Id = Guid.NewGuid(),
            //    CustomerId = customerId,
            //    Action = action,
            //    Properties = properties ?? new(),
            //    Timestamp = DateTime.UtcNow
            //};

            //await _context.UserBehaviors.AddAsync(behavior, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task TrackPerformanceAsync(
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

            await TrackMetricAsync($"performance.{operationName}.duration", duration.TotalMilliseconds, new Dictionary<string, string>
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
