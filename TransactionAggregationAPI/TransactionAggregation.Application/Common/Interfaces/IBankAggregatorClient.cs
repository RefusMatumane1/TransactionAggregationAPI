using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    /// <summary>
    /// Client for a licensed South African account-data aggregator (e.g. Stitch), which
    /// itself holds the agreements with FNB/Standard Bank/Absa/Capitec — we never talk to
    /// those banks directly. Backs only the OAuth consent/linking handshake
    /// (InitiateBankLink/CompleteBankLink) — the aggregator pushes transaction data to us via
    /// WebhookEndpoints rather than us pulling it through this client.
    /// </summary>
    public interface IBankAggregatorClient
    {
        /// <summary>Builds the URL to redirect the customer to for bank-consent authorization.</summary>
        string BuildAuthorizationUrl(Institution institution, string state);

        Task<AggregatorTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<AggregatorTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

        /// <summary>Fetches the details of the account the customer just consented to share.</summary>
        Task<AggregatorLinkedAccountResult> GetLinkedAccountAsync(string accessToken, CancellationToken cancellationToken = default);
    }
}
