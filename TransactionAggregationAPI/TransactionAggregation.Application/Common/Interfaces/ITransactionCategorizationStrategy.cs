using System;
using System.Collections.Generic;
using System.Text;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    /// <summary>
    /// Strategy for automatic transaction categorization
    /// </summary>
    public interface ITransactionCategorizationStrategy
    {
        Task<TransactionCategory> CategorizeAsync(
            string description,
            Money amount,
            TransactionSource source,
            CancellationToken cancellationToken = default);

        Task<TransactionCategory> CategorizeWithMLAsync(
            string description,
            Money amount,
            TransactionSource source,
            Dictionary<string, string> metadata,
            CancellationToken cancellationToken = default);

        Task TrainModelAsync(IEnumerable<Transaction> historicalTransactions, CancellationToken cancellationToken = default);
        double GetConfidenceScore(string description, TransactionCategory category);
    }

    /// <summary>
    /// Rule-based categorization strategy (primary)
    /// </summary>
    public interface IRuleBasedCategorizationStrategy : ITransactionCategorizationStrategy
    {
        void AddRule(CategorizationRule rule);
        void RemoveRule(string ruleId);
        IEnumerable<CategorizationRule> GetRules();
    }

    public record CategorizationRule
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string Pattern { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public int Priority { get; init; }
        public bool IsRegex { get; init; }
        public string? Description { get; init; }
    }
}
