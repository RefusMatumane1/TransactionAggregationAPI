namespace TransactionAggregationUI.Models.Transactions
{
    public class TransactionModel
    {
        public Guid Id { get; set; }
        public string ExternalAccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TransactionCategory Category { get; set; }

        public string Source { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}