namespace TransactionAggregationUI.Models.Transactions;

public class TransactionSummaryModel
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance { get; set; }
    public Dictionary<string, decimal> SpendingByCategory { get; set; } = new();
    public Dictionary<string, decimal> SpendingByMonth { get; set; } = new();
    public int TotalTransactions { get; set; }
    public int CompletedTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public TransactionPeriodModel Period { get; set; } = new();
}

public class TransactionPeriodModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days { get; set; }
}
