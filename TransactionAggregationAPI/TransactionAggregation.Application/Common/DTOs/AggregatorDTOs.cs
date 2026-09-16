namespace TransactionAggregation.Application.Common.DTOs
{
    public sealed record AggregatorTokenResult(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAtUtc);

    public sealed record AggregatorLinkedAccountResult(
    string ExternalAccountId,
    string AccountNumber,
    string AccountName,
    string AccountType,
    string Currency);
}