using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Services
{
    public class TransactionAggregator : ITransactionAggregator
    {
        private readonly IEnumerable<ITransactionSource> _transactionSources;
        private readonly ILogger<TransactionAggregator> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public TransactionAggregator(
            IEnumerable<ITransactionSource> transactionSources,
            ILogger<TransactionAggregator> logger)
        {
            _transactionSources = transactionSources;
            _logger = logger;

            // Configure retry policy
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Retry {RetryCount} after {Delay}ms for transaction source",
                            retryCount,
                            timeSpan.TotalMilliseconds);
                    });
        }

        public async Task<IReadOnlyList<Transaction>> AggregateCustomerTransactionsAsync(
            CustomerId customerId,
            CancellationToken cancellationToken = default)
        {
            var fromDate = DateTime.UtcNow.AddMonths(-3); // Last 3 months
            var toDate = DateTime.UtcNow;

            var sourceTasks = _transactionSources.Select(async source =>
            {
                try
                {
                    return await _retryPolicy.ExecuteAsync(async () =>
                        await source.GetTransactionsAsync(customerId, fromDate, toDate, cancellationToken));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get transactions from source {SourceName}", source.SourceName);
                    return new List<ExternalTransactionDTO>();
                }
            });

            var externalTransactions = await Task.WhenAll(sourceTasks);
            var allTransactions = externalTransactions.SelectMany(x => x).ToList();

            _logger.LogInformation(
                "Retrieved {TotalCount} transactions from {SourceCount} sources for customer {CustomerId}",
                allTransactions.Count,
                _transactionSources.Count(),
                customerId);

            // Remove duplicates based on transaction ID
            var uniqueTransactions = allTransactions
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // Map to domain entities with categorization
            var domainTransactions = uniqueTransactions
                .Select(t => MapToDomainTransaction(customerId, t))
                .ToList();

            return domainTransactions;
        }

        private static Transaction MapToDomainTransaction(CustomerId customerId, ExternalTransactionDTO external)
        {
            return Transaction.Create(
                customerId,
                Money.Create(external.Amount, external.Currency),
                external.Description,
                CategorizeTransaction(external),
                TransactionSource.Create(external.Id, external.Id));
        }

        private static TransactionCategory CategorizeTransaction(ExternalTransactionDTO external)
        {
            // Simple categorization logic based on description and amount
            var description = external.Description?.ToLower() ?? "";
            var amount = external.Amount;

            // Income detection
            if (amount > 0)
                return TransactionCategory.Income;

            // Expense categorization
            return description switch
            {
                var d when d.Contains("grocery") || d.Contains("supermarket") => TransactionCategory.Groceries,
                var d when d.Contains("uber") || d.Contains("taxi") || d.Contains("bus") || d.Contains("train") => TransactionCategory.Transportation,
                var d when d.Contains("electric") || d.Contains("water") || d.Contains("gas") || d.Contains("internet") => TransactionCategory.Utilities,
                var d when d.Contains("netflix") || d.Contains("spotify") || d.Contains("cinema") || d.Contains("theater") => TransactionCategory.Entertainment,
                var d when d.Contains("amazon") || d.Contains("mall") || d.Contains("store") => TransactionCategory.Shopping,
                _ => TransactionCategory.Uncategorized
            };
        }

        Task<IReadOnlyList<Transaction>> ITransactionAggregator.AggregateCustomerTransactionsAsync(
            Guid customerId,
            DateTime? FromDate,
            DateTime? ToDate,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
