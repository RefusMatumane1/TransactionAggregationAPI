using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Features.Transactions.DTOs
{
    public sealed record TransactionDto
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = null!;
        public string FormattedAmount => $"{Currency} {Amount:N2}";
        public string Description { get; init; } = null!;
        public TransactionCategory Category { get; init; }
        public string CategoryName => Category.ToString();
        public TransactionStatus Status { get; init; }
        public string StatusName => Status.ToString();
        public string Source { get; init; } = null!;
        public DateTime Date { get; init; }
        public DateTime CreatedAt { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();

        // Helper properties
        public bool IsExpense => Amount < 0;
        public bool IsIncome => Amount > 0;
        public string Age => GetAge();

        private string GetAge()
        {
            var days = (DateTime.UtcNow - Date).Days;
            return days switch
            {
                0 => "Today",
                1 => "Yesterday",
                < 7 => $"{days} days ago",
                < 30 => $"{days / 7} weeks ago",
                < 365 => $"{days / 30} months ago",
                _ => $"{days / 365} years ago"
            };
        }
    }
}
