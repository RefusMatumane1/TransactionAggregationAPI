namespace TransactionAggregationUI.Models.Transactions;

public class CreateTransactionModel
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
}
