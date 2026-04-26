using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services
{
    public class TransactionCategorizationService : ITransactionCategorizationService
    {
        private readonly Dictionary<string, TransactionCategory> _keywords = new()
        {
            ["walmart"] = TransactionCategory.Groceries,
            ["kroger"] = TransactionCategory.Groceries,
            ["restaurant"] = TransactionCategory.Dining,
            ["starbucks"] = TransactionCategory.Dining,
            ["uber"] = TransactionCategory.Transportation,
            ["lyft"] = TransactionCategory.Transportation,
            ["netflix"] = TransactionCategory.Entertainment,
            ["spotify"] = TransactionCategory.Entertainment,
            ["electric"] = TransactionCategory.Utilities,
            ["water"] = TransactionCategory.Utilities,
            ["rent"] = TransactionCategory.Housing,
            ["mortgage"] = TransactionCategory.Housing
        };

        public Task<TransactionCategory> CategorizeTransactionAsync(Transaction transaction, CancellationToken cancellationToken)
        {
            var description = transaction.Description.ToLowerInvariant();

            foreach (var (keyword, category) in _keywords)
            {
                if (description.Contains(keyword))
                    return Task.FromResult(category);
            }

            // Categorize based on amount sign
            if (transaction.Amount.Amount > 0)
                return Task.FromResult(TransactionCategory.Income);

            return Task.FromResult(TransactionCategory.Uncategorized);
        }
    }
}
