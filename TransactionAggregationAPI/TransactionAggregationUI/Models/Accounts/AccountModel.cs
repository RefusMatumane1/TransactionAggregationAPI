namespace TransactionAggregationUI.Models.Accounts;

public class AccountModel
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    /// <summary>Posted (settled) transactions only.</summary>
    public decimal Balance { get; set; }

    /// <summary>Net of pending authorisations, not yet in Balance.</summary>
    public decimal PendingBalance { get; set; }

    /// <summary>Balance less pending outflows.</summary>
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}