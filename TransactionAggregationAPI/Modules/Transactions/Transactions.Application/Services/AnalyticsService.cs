using Microsoft.Extensions.Logging;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Services
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

            TrackMetric("transactions.created", new Dictionary<string, string>
            {
                ["currency"] = transaction.Amount.Currency,
                ["category"] = transaction.Category.ToString(),
                ["is_income"] = transaction.IsIncome.ToString()
            });

            return Task.CompletedTask;
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

            TrackMetric("transactions.categorized", new Dictionary<string, string>
            {
                ["old_category"] = oldCategory.ToString(),
                ["new_category"] = newCategory.ToString(),
                ["is_auto"] = isAuto.ToString()
            });

            return Task.CompletedTask;
        }

        public Task TrackTransactionSyncedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            TrackMetric("transactions.synced", new Dictionary<string, string>
            {
                ["source"] = transaction.Source.Name
            });

            return Task.CompletedTask;
        }

        private void TrackMetric(string metricName, Dictionary<string, string> tags) =>
            _logger.LogDebug("[Metric] {MetricName}: 1, Tags: {@Tags}", metricName, tags);
    }
}