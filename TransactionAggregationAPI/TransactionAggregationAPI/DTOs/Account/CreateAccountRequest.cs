using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs.Account
{
    public sealed record CreateAccountRequest(
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string Currency = "ZAR");
}
