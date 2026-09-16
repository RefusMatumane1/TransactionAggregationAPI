using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Interfaces
{
    public interface IBankAggregatorClient
    {
        string BuildAuthorizationUrl(Institution institution, string state);

        Task<AggregatorTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<AggregatorTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task<AggregatorLinkedAccountResult> GetLinkedAccountAsync(string accessToken, CancellationToken cancellationToken = default);
    }
}