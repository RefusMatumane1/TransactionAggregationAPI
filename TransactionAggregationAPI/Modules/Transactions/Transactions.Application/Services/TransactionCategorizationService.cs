using Microsoft.Extensions.Options;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Services
{
    public sealed class TransactionCategorizationService : ITransactionCategorizationService
    {
        private sealed record Rule(string Keyword, string[] Words, TransactionCategory Category);

        private readonly IReadOnlyList<Rule> _rules;

        public TransactionCategorizationService(IOptions<CategorizationOptions> options)
        {
            _rules = BuildRules(options.Value.Keywords);
        }

        // Deterministic: the longest matching keyword wins (ties broken by ordinal order), then the
        // bank's own category, then the direction of the amount.
        public TransactionCategory Categorize(string description, decimal amount, TransactionCategory? bankCategory)
        {
            var words = Words(description);
            foreach (var rule in _rules)
            {
                if (ContainsSequence(words, rule.Words))
                    return rule.Category;
            }

            if (bankCategory is { } fromBank && fromBank != TransactionCategory.Uncategorized)
                return fromBank;

            return amount > 0 ? TransactionCategory.Income : TransactionCategory.Uncategorized;
        }

        private static List<Rule> BuildRules(Dictionary<string, string> keywords) =>
            keywords
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Select(kv => (Keyword: kv.Key.Trim().ToLowerInvariant(), CategoryName: kv.Value))
                .Where(kv => Enum.TryParse<TransactionCategory>(kv.CategoryName, ignoreCase: true, out _))
                .DistinctBy(kv => kv.Keyword)
                .OrderByDescending(kv => kv.Keyword.Length)
                .ThenBy(kv => kv.Keyword, StringComparer.Ordinal)
                .Select(kv => new Rule(
                    kv.Keyword,
                    Words(kv.Keyword),
                    Enum.Parse<TransactionCategory>(kv.CategoryName, ignoreCase: true)))
                .Where(rule => rule.Words.Length > 0)
                .ToList();

        private static string[] Words(string text)
        {
            var words = new List<string>();
            var start = -1;
            for (var i = 0; i <= text.Length; i++)
            {
                var isWordChar = i < text.Length && char.IsLetterOrDigit(text[i]);
                if (isWordChar && start < 0)
                    start = i;
                else if (!isWordChar && start >= 0)
                {
                    words.Add(text[start..i].ToLowerInvariant());
                    start = -1;
                }
            }

            return [.. words];
        }

        private static bool ContainsSequence(string[] words, string[] phrase)
        {
            for (var i = 0; i + phrase.Length <= words.Length; i++)
            {
                var j = 0;
                while (j < phrase.Length && words[i + j] == phrase[j])
                    j++;
                if (j == phrase.Length)
                    return true;
            }

            return false;
        }
    }
}