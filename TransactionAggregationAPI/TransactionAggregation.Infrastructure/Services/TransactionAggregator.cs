using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Services
{
    public class TransactionAggregator : ITransactionAggregator
    {
        private readonly IEnumerable<ITransactionSource> _transactionSources;
        private readonly ITransactionCategorizationService _categorizationService;
        private readonly ILogger<TransactionAggregator> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;

        public TransactionAggregator(
            IEnumerable<ITransactionSource> transactionSources,
            ITransactionCategorizationService categorizationService,
            ILogger<TransactionAggregator> logger)
        {
            _transactionSources = transactionSources;
            _categorizationService = categorizationService;
            _logger = logger;

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

        public async Task<AggregationResult> AggregateCustomerTransactionsAsync(
            Guid customerId,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken cancellationToken = default)
        {
            var customerIdVO = CustomerId.CreateFrom(customerId);
            var resolvedFrom = fromDate ?? DateTime.UtcNow.AddMonths(-3);
            var resolvedTo = toDate ?? DateTime.UtcNow;

            var sourceList = _transactionSources.ToList();

            _logger.LogInformation(
                "Aggregating transactions for customer {CustomerId} from {SourceCount} sources between {From} and {To}",
                customerId, sourceList.Count, resolvedFrom, resolvedTo);

            var sourceTasks = sourceList.Select(async source =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var results = await _retryPolicy.ExecuteAsync(async () =>
                        await source.GetTransactionsAsync(customerIdVO, resolvedFrom, resolvedTo, cancellationToken));
                    sw.Stop();
                    return (Source: source.SourceName, Transactions: results, IsSuccess: true, Error: (string?)null, Duration: sw.Elapsed);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogError(ex, "Failed to get transactions from source {SourceName}", source.SourceName);
                    return (Source: source.SourceName, Transactions: (IReadOnlyList<ExternalTransactionDTO>)new List<ExternalTransactionDTO>(), IsSuccess: false, Error: ex.Message, Duration: sw.Elapsed);
                }
            });

            var sourceOutcomes = await Task.WhenAll(sourceTasks);

            var sourceResults = sourceOutcomes.Select(o => new SourceAggregationResult
            {
                SourceName = o.Source,
                TransactionsFound = o.Transactions.Count,
                IsSuccess = o.IsSuccess,
                Error = o.Error,
                Duration = o.Duration
            }).ToList();

            var allTransactions = sourceOutcomes.SelectMany(o => o.Transactions).ToList();

            _logger.LogInformation(
                "Retrieved {TotalCount} raw transactions from {SourceCount} sources for customer {CustomerId}",
                allTransactions.Count, sourceList.Count, customerId);

            // Deduplicate by external ID
            var uniqueTransactions = allTransactions
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // Map to domain entities then categorize via the shared categorization service
            var domainTransactions = new List<Transaction>(uniqueTransactions.Count);

            foreach (var external in uniqueTransactions)
            {
                var transaction = Transaction.Create(
                    customerIdVO,
                    Money.Create(external.Amount, external.Currency),
                    external.Description,
                    TransactionCategory.Uncategorized,
                    TransactionSource.Create(external.Id, external.Id));

                var category = await _categorizationService.CategorizeTransactionAsync(
                    transaction, cancellationToken);

                if (category != TransactionCategory.Uncategorized)
                    transaction.Categorize(category);

                domainTransactions.Add(transaction);
            }

            return new AggregationResult
            {
                Transactions = domainTransactions,
                SourceResults = sourceResults
            };
        }
    }
}
