using BuildingBlocks.Application.Pagination;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using System.Linq.Expressions;

namespace Modules.Transactions.Application.Common.Pagination
{
    // Only orders backed by an index keyed (sort column, Id), so deep pages stay range scans.
    public static class TransactionSorts
    {
        public const string Date = "date";
        public const string Amount = "amount";

        private static readonly IReadOnlyDictionary<string, KeysetSort<Transaction>> All =
            new Dictionary<string, KeysetSort<Transaction>>(StringComparer.OrdinalIgnoreCase)
            {
                [Date] = By(Date, t => t.Date, KeyCodecs.UtcDateTime),
                [Amount] = By(Amount, t => t.Amount.Amount, KeyCodecs.Decimal)
            };

        public static IReadOnlyCollection<string> Names { get; } = All.Keys.ToList();

        public static bool IsKnown(string? name) => name is null || All.ContainsKey(name);

        public static KeysetSort<Transaction> Resolve(string? name) =>
            name is not null && All.TryGetValue(name, out var sort) ? sort : All[Date];

        private static KeysetSort<Transaction> By<TKey>(
            string name, Expression<Func<Transaction, TKey>> key, KeyCodec<TKey> codec) =>
            new KeysetSort<Transaction, TKey, TransactionId>(
                name, key, t => t.Id, id => id.Value, TransactionId.CreateFrom, codec);
    }
}