using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Consent;

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresInSeconds);

/// <summary>
/// The aggregator's memory. Authorization codes and tokens live only in memory — the
/// application uses them once, while completing a link. Consents (which accounts the
/// aggregator may push for) are persisted, because the feed runs on them indefinitely.
/// A joint account is consented once, however many customers link it: the aggregator
/// pushes each transaction once and the application gives every holder a copy.
/// </summary>
public sealed class ConsentStore
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private const int AccessTokenLifetimeSeconds = 3600;

    private sealed record PendingCode(string AccountId, string ClientId, string RedirectUri, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, PendingCode> _codes = new();
    private readonly ConcurrentDictionary<string, string> _accessTokens = new();
    private readonly ConcurrentDictionary<string, string> _refreshTokens = new();
    private readonly HashSet<string> _consented;
    private readonly Lock _consentLock = new();
    private readonly string _file;
    private readonly TimeProvider _time;
    private readonly ILogger<ConsentStore> _logger;

    public ConsentStore(
        IOptions<MockAggregatorOptions> options, IHostEnvironment environment, TimeProvider time, ILogger<ConsentStore> logger)
    {
        var directory = Path.Combine(environment.ContentRootPath, options.Value.DataDirectory);
        Directory.CreateDirectory(directory);
        _file = Path.Combine(directory, "consents.json");
        _time = time;
        _logger = logger;
        _consented = File.Exists(_file)
            ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(_file)) ?? []
            : [];
    }

    public string IssueCode(string accountId, string clientId, string redirectUri)
    {
        var code = NewSecret();
        _codes[code] = new PendingCode(accountId, clientId, redirectUri, _time.GetUtcNow().UtcDateTime + CodeLifetime);
        return code;
    }

    /// <summary>Single use: a code is consumed whether or not it turns out to be valid.</summary>
    public bool TryRedeemCode(string code, string clientId, string redirectUri, out string accountId)
    {
        accountId = string.Empty;
        if (!_codes.TryRemove(code, out var pending))
            return false;
        if (pending.ExpiresAt < _time.GetUtcNow().UtcDateTime || pending.ClientId != clientId || pending.RedirectUri != redirectUri)
            return false;

        accountId = pending.AccountId;
        return true;
    }

    /// <summary>Issuing tokens is the moment the application completes the link — the consent starts here.</summary>
    public TokenPair IssueTokens(string accountId)
    {
        var tokens = new TokenPair(NewSecret(), NewSecret(), AccessTokenLifetimeSeconds);
        _accessTokens[tokens.AccessToken] = accountId;
        _refreshTokens[tokens.RefreshToken] = accountId;
        Grant(accountId);
        return tokens;
    }

    /// <summary>Rotates: the old refresh token stops working.</summary>
    public bool TryRefresh(string refreshToken, out TokenPair tokens)
    {
        tokens = null!;
        if (!_refreshTokens.TryRemove(refreshToken, out var accountId))
            return false;

        tokens = IssueTokens(accountId);
        return true;
    }

    public MockAccount? AccountForAccessToken(string accessToken) =>
        _accessTokens.TryGetValue(accessToken, out var accountId) ? MockCatalog.FindAccount(accountId) : null;

    public IReadOnlyList<MockAccount> ConsentedAccounts()
    {
        lock (_consentLock)
            return _consented.Select(MockCatalog.FindAccount).OfType<MockAccount>().ToList();
    }

    public bool Revoke(string accountId)
    {
        lock (_consentLock)
        {
            if (!_consented.Remove(accountId))
                return false;
            Persist();
        }

        _logger.LogInformation("Consent for {Account} revoked — the feed stops for every holder", accountId);
        return true;
    }

    private void Grant(string accountId)
    {
        lock (_consentLock)
        {
            if (!_consented.Add(accountId))
                return;
            Persist();
        }

        _logger.LogInformation("Consent granted for {Account} — the feed now pushes its transactions", accountId);
    }

    private void Persist() => File.WriteAllText(_file, JsonSerializer.Serialize(_consented));

    private static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}