namespace TransactionAggregation.Application.Common.DTOs
{
    public sealed record AggregatorTokenResult(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAtUtc);

    /// <summary>The specific external account the customer consented to share, as returned
    /// by the aggregator right after the OAuth exchange.</summary>
    public sealed record AggregatorLinkedAccountResult(
        string ExternalAccountId,
        string AccountNumber,
        string AccountName,
        string AccountType,
        string Currency);
}
