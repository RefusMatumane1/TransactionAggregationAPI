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

        // The code of the bank that delivered it (e.g. "FNB"); BankDirectory gives its name and colour.
        public string Source { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }
}