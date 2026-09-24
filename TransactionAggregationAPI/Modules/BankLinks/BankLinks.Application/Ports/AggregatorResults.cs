namespace Modules.BankLinks.Application.Ports
{
    /// <summary>What <see cref="IBankAggregatorClient"/> returns: the aggregator's view, not this module's API shape.</summary>
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