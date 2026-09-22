using Modules.BankLinks.Application.DTOs;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Application.Ports
{
    public interface IBankAggregatorClient
    {
        string BuildAuthorizationUrl(Institution institution, string state);

        Task<AggregatorTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<AggregatorTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task<AggregatorLinkedAccountResult> GetLinkedAccountAsync(string accessToken, CancellationToken cancellationToken = default);
    }
}
