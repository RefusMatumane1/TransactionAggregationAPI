namespace TransactionAggregationUI.Models.Accounts;

public class CreateAccountModel
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; } = AccountType.Checking;
    public string Currency { get; set; } = "ZAR";
}
