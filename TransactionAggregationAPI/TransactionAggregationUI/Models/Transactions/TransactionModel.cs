namespace TransactionAggregationUI.Models.Transactions;

public class TransactionModel
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionCategory Category { get; set; }
    public TransactionStatus Status { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
