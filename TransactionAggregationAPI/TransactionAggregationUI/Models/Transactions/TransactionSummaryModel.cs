namespace TransactionAggregationUI.Models.Transactions;

/// <summary>
/// Mirrors the API's TransactionSummaryDto. Money figures count posted (settled)
/// transactions only; pending authorisations are in the Pending* fields.
/// </summary>
public class TransactionSummaryModel
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance { get; set; }
    public Dictionary<string, decimal> SpendingByCategory { get; set; } = new();
    public List<MonthlySummaryModel> MonthlySummaries { get; set; } = [];
    public int TotalTransactions { get; set; }
    public int CompletedTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public decimal PendingIncome { get; set; }
    public decimal PendingExpenses { get; set; }
}

public class MonthlySummaryModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetBalance { get; set; }
    public int TransactionCount { get; set; }
}