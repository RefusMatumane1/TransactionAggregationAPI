using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.BankLinks.Domain.ValueObjects;
using Modules.BankLinks.Infrastructure.Providers;
using System.Net;
using TransactionAggregation.MockAggregator;
using Xunit;

namespace TransactionAggregation.Tests.Integration.MockBanks;

public sealed class MockAggregatorFactory : WebApplicationFactory<MockAggregatorOptions>
{
    public const string ClientId = "test-client";
    public const string ClientSecret = "test-client-secret";
    public const string RedirectUri = "https://app.test/api/v1/bank-links/callback";

    public string DataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "mock-aggregator-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Host settings, so Program sees them before it builds (it reads Feed options early).
        builder.UseSetting("MockAggregator:ClientId", ClientId);
        builder.UseSetting("MockAggregator:ClientSecret", ClientSecret);
        builder.UseSetting("MockAggregator:AllowedRedirectUris:0", RedirectUri);
        builder.UseSetting("MockAggregator:DataDirectory", DataDirectory);
        builder.UseSetting("Feed:Enabled", "false");
        builder.UseSetting("ConnectionStrings:seq", "http://localhost:5341"); // required by ServiceDefaults
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(DataDirectory))
            Directory.Delete(DataDirectory, recursive: true);
    }
}

/// <summary>
/// The seam between BankLinks and the mock: the application's real HttpBankAggregatorClient
/// completes a link against the running mock, exactly as CompleteBankLinkCommandHandler does.
/// </summary>
public class MockAggregatorOAuthTests : IClassFixture<MockAggregatorFactory>
{
    private readonly HttpClient _http;

    public MockAggregatorOAuthTests(MockAggregatorFactory factory)
    {
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private HttpBankAggregatorClient AppClient(string clientSecret = MockAggregatorFactory.ClientSecret) => new(
        _http,
        Options.Create(new BankAggregatorOptions
        {
            ClientId = MockAggregatorFactory.ClientId,
            ClientSecret = clientSecret,
            AuthorizeEndpoint = "/oauth/authorize",
            TokenEndpoint = "/oauth/token",
            AccountEndpoint = "/accounts/me",
            RedirectUri = MockAggregatorFactory.RedirectUri
        }),
        NullLogger<HttpBankAggregatorClient>.Instance);

    private async Task<HttpResponseMessage> DecideAsync(string accountId, string decision, string redirectUri = MockAggregatorFactory.RedirectUri) =>
        await _http.PostAsync("/oauth/authorize", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = MockAggregatorFactory.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = "state-123",
            ["account_id"] = accountId,
            ["decision"] = decision
        }));

    private static Dictionary<string, string> RedirectQuery(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith(MockAggregatorFactory.RedirectUri);
        return QueryHelpers.ParseQuery(new Uri(location).Query).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
    }

    private async Task<string> ConsentAsync(string accountId) => RedirectQuery(await DecideAsync(accountId, "allow"))["code"];

    [Fact]
    public async Task TheApplicationsClient_CompletesALinkAgainstTheMock()
    {
        var client = AppClient();

        var consentPage = await _http.GetAsync(client.BuildAuthorizationUrl(Institution.FNB, "state-123"));
        consentPage.StatusCode.Should().Be(HttpStatusCode.OK);
        (await consentPage.Content.ReadAsStringAsync()).Should().Contain("FNB Joint Household Account").And.Contain("joint account");

        var redirect = RedirectQuery(await DecideAsync("mock-fnb-joint-1003", "allow"));
        redirect["state"].Should().Be("state-123", "the application matches the callback to its pending link by state");

        var tokens = await client.ExchangeAuthorizationCodeAsync(redirect["code"]);
        var account = await client.GetLinkedAccountAsync(tokens.AccessToken);

        account.ExternalAccountId.Should().Be("mock-fnb-joint-1003");
        account.AccountType.Should().Be("checking");
        account.Currency.Should().Be("ZAR");
        (await _http.GetStringAsync("/consents")).Should().Contain("mock-fnb-joint-1003", "completing the link starts the feed for the account");
    }

    [Fact]
    public async Task AuthorizationCode_CanBeUsedOnlyOnce()
    {
        var client = AppClient();
        var code = await ConsentAsync("mock-capitec-chq-3001");
        await client.ExchangeAuthorizationCodeAsync(code);

        var replay = () => client.ExchangeAuthorizationCodeAsync(code);

        await replay.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task WrongClientSecret_IsRejectedAsUnauthorized()
    {
        var code = await ConsentAsync("mock-absa-chq-2001");

        var exchange = () => AppClient(clientSecret: "not-the-secret").ExchangeAuthorizationCodeAsync(code);

        await exchange.Should().ThrowAsync<BankAggregatorUnauthorizedException>();
    }

    [Fact]
    public async Task UnregisteredRedirectUri_IsNeverRedirectedTo()
    {
        var consentPage = await _http.GetAsync(
            $"/oauth/authorize?client_id={MockAggregatorFactory.ClientId}&redirect_uri=https%3A%2F%2Fevil.test%2Fcb" +
            "&response_type=code&state=s&institution=FNB");
        var decision = await DecideAsync("mock-fnb-chq-1001", "allow", redirectUri: "https://evil.test/cb");

        consentPage.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        decision.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        decision.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task DeniedConsent_RedirectsWithAccessDenied_AndStartsNoFeed()
    {
        var redirect = RedirectQuery(await DecideAsync("mock-sbsa-chq-4001", "deny"));

        redirect["error"].Should().Be("access_denied");
        redirect.Should().NotContainKey("code");
        (await _http.GetStringAsync("/consents")).Should().NotContain("mock-sbsa-chq-4001");
    }

    [Fact]
    public async Task AccountEndpoint_RequiresAValidAccessToken()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/accounts/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "forged-token");

        (await _http.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}