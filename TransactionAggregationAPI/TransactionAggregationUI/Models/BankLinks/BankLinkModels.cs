namespace TransactionAggregationUI.Models.BankLinks;

/// <summary>Mirrors the API's Institution values (sent and received as numbers).</summary>
public enum Institution
{
    FNB = 0,
    StandardBank = 1,
    Absa = 2,
    Capitec = 3
}

/// <summary>Mirrors the API's BankLinkStatus values.</summary>
public enum BankLinkStatus
{
    PendingAuthorization = 0,
    Active = 1,
    NeedsReauthorization = 2,
    Revoked = 3
}

public class BankLinkModel
{
    public Guid Id { get; set; }
    public Institution Institution { get; set; }
    public BankLinkStatus Status { get; set; }
    public Guid? AccountId { get; set; }
    public DateTime LinkedAt { get; set; }
}

public class InitiateBankLinkResultModel
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

public class CompleteBankLinkResultModel
{
    public Guid AccountId { get; set; }
}

public static class InstitutionNames
{
    public static string DisplayName(this Institution institution) => institution switch
    {
        Institution.StandardBank => "Standard Bank",
        _ => institution.ToString()
    };
}