using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Services
{
    public class TransactionCategorizationService : ITransactionCategorizationService
    {
        private readonly Dictionary<string, TransactionCategory> _rules;

        public TransactionCategorizationService(IOptions<CategorizationOptions> options)
        {
            _rules = BuildRules(options.Value.Keywords);
        }

        public Task<TransactionCategory> CategorizeTransactionAsync(
            Transaction transaction,
            CancellationToken cancellationToken)
        {
            var description = transaction.Description.ToLowerInvariant();

            foreach (var (keyword, category) in _rules)
            {
                if (description.Contains(keyword))
                    return Task.FromResult(category);
            }

            // Fall back to sign-based categorization
            if (transaction.Amount.Amount > 0)
                return Task.FromResult(TransactionCategory.Income);

            return Task.FromResult(TransactionCategory.Uncategorized);
        }

        private static Dictionary<string, TransactionCategory> BuildRules(
            Dictionary<string, string> keywords)
        {
            var rules = new Dictionary<string, TransactionCategory>(StringComparer.OrdinalIgnoreCase);

            foreach (var (keyword, categoryName) in keywords)
            {
                if (Enum.TryParse<TransactionCategory>(categoryName, ignoreCase: true, out var category))
                    rules[keyword.ToLowerInvariant()] = category;
            }

            return rules;
        }
    }
}
