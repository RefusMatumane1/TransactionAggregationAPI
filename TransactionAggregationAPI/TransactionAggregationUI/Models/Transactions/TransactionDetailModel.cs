namespace TransactionAggregationUI.Models.Transactions
{
    // GET /api/v1/transactions/{id}: one ledger entry with where it came from.
    public class TransactionDetailModel
    {
        public Guid Id { get; set; }
        public string Institution { get; set; } = string.Empty;
        public string ExternalAccountId { get; set; } = string.Empty;
        public string ExternalTransactionId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public TransactionCategory Category { get; set; }
        public DateTime RecordedAt { get; set; }

        // The bank's own wording before normalisation ("bankDescription", "bankCategory"), when it differed.
        public Dictionary<string, string> Metadata { get; set; } = [];

        public const string BankDescriptionKey = "bankDescription";
        public const string BankCategoryKey = "bankCategory";
    }
}