namespace TransactionAggregationUI.Models.Transactions;

public class TransactionFilterParams
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public TransactionCategory? Category { get; set; }
    public TransactionStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? SearchTerm { get; set; }
    public string? Source { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}
