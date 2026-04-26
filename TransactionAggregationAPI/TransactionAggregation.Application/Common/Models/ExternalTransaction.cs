namespace TransactionAggregation.Application.Common.Models
{
    public sealed record ExternalTransaction
    {
        public required string Id { get; init; }
        public required decimal Amount { get; init; }
        public required string Currency { get; init; }
        public required string Description { get; init; }
        public required string Category { get; init; }
        public required DateTime Date { get; init; }
        public string? MerchantName { get; init; }
        public string? Location { get; init; }
        public string? PaymentMethod { get; init; }
        public Dictionary<string, string> Metadata { get; init; } = new();

        public bool IsExpense => Amount < 0;
        public bool IsIncome => Amount > 0;
        public decimal AbsoluteAmount => Math.Abs(Amount);
    }
}
