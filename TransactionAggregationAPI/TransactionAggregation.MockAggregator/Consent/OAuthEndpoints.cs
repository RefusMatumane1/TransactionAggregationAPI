using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Consent;

/// <summary>
/// The three endpoints the application's HttpBankAggregatorClient calls — authorize (a
/// browser page), token and account — plus two developer conveniences for consents.
/// Forms are read directly rather than bound, so no antiforgery machinery is involved; the
/// consent page is a development stand-in, not a hardened login.
/// </summary>
public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/oauth/authorize", ShowConsentPage);
        app.MapPost("/oauth/authorize", DecideConsentAsync);
        app.MapPost("/oauth/token", IssueTokenAsync);
        app.MapGet("/accounts/me", GetLinkedAccount);

        app.MapGet("/consents", (ConsentStore store) => Results.Ok(store.ConsentedAccounts()));
        app.MapDelete("/consents/{accountId}", (string accountId, ConsentStore store) =>
            store.Revoke(accountId) ? Results.NoContent() : Results.NotFound());
    }

    private static IResult ShowConsentPage(
        HttpRequest request, IOptions<MockAggregatorOptions> options)
    {
        var query = request.Query;
        string clientId = query["client_id"].ToString(), redirectUri = query["redirect_uri"].ToString(),
               state = query["state"].ToString(), institution = query["institution"].ToString();

        // Never redirect to a URI we haven't vetted — show the problem instead.
        if (!IsKnownClient(options.Value, clientId, redirectUri))
            return Results.BadRequest("Unknown client_id or redirect_uri.");
        if (query["response_type"] != "code" || string.IsNullOrWhiteSpace(state))
            return Results.BadRequest("response_type must be 'code' and state is required.");

        var accounts = MockCatalog.AccountsAt(institution);
        if (accounts.Count == 0)
            return Results.BadRequest($"The mock aggregator has no accounts at '{institution}'.");

        return Results.Content(RenderConsentPage(clientId, redirectUri, state, institution, accounts), "text/html");
    }

    private static async Task<IResult> DecideConsentAsync(
        HttpRequest request, ConsentStore store, IOptions<MockAggregatorOptions> options)
    {
        var form = await request.ReadFormAsync();
        string clientId = form["client_id"].ToString(), redirectUri = form["redirect_uri"].ToString(),
               state = form["state"].ToString(), accountId = form["account_id"].ToString();

        if (!IsKnownClient(options.Value, clientId, redirectUri))
            return Results.BadRequest("Unknown client_id or redirect_uri.");

        if (form["decision"] != "allow")
            return Results.Redirect(QueryHelpers.AddQueryString(redirectUri,
                new Dictionary<string, string?> { ["error"] = "access_denied", ["state"] = state }));

        if (MockCatalog.FindAccount(accountId) is null)
            return Results.BadRequest("Choose an account to link.");

        var code = store.IssueCode(accountId, clientId, redirectUri);
        return Results.Redirect(QueryHelpers.AddQueryString(redirectUri,
            new Dictionary<string, string?> { ["code"] = code, ["state"] = state }));
    }

    private static async Task<IResult> IssueTokenAsync(
        HttpRequest request, ConsentStore store, IOptions<MockAggregatorOptions> options)
    {
        var form = await request.ReadFormAsync();

        if (!IsAuthenticatedClient(options.Value, form["client_id"].ToString(), form["client_secret"].ToString()))
            return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

        switch (form["grant_type"].ToString())
        {
            case "authorization_code":
                if (!store.TryRedeemCode(form["code"].ToString(), form["client_id"].ToString(),
                        form["redirect_uri"].ToString(), out var accountId))
                    return Results.BadRequest(new { error = "invalid_grant" });
                return TokenResponse(store.IssueTokens(accountId));

            case "refresh_token":
                return store.TryRefresh(form["refresh_token"].ToString(), out var refreshed)
                    ? TokenResponse(refreshed)
                    : Results.BadRequest(new { error = "invalid_grant" });

            default:
                return Results.BadRequest(new { error = "unsupported_grant_type" });
        }
    }

    private static IResult GetLinkedAccount(HttpRequest request, ConsentStore store)
    {
        var header = request.Headers.Authorization.ToString();
        var account = header.StartsWith("Bearer ", StringComparison.Ordinal)
            ? store.AccountForAccessToken(header["Bearer ".Length..])
            : null;

        return account is null
            ? Results.Unauthorized()
            : Results.Ok(new
            {
                id = account.Id,
                accountNumber = account.AccountNumber,
                accountName = account.AccountName,
                accountType = account.AccountType,
                currency = account.Currency
            });
    }

    private static IResult TokenResponse(TokenPair tokens) => Results.Ok(new
    {
        access_token = tokens.AccessToken,
        refresh_token = tokens.RefreshToken,
        expires_in = tokens.ExpiresInSeconds,
        token_type = "Bearer"
    });

    private static bool IsKnownClient(MockAggregatorOptions options, string clientId, string redirectUri) =>
        clientId == options.ClientId && options.AllowedRedirectUris.Contains(redirectUri, StringComparer.Ordinal);

    private static bool IsAuthenticatedClient(MockAggregatorOptions options, string clientId, string clientSecret) =>
        clientId == options.ClientId
        && !string.IsNullOrEmpty(options.ClientSecret)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(clientSecret), Encoding.UTF8.GetBytes(options.ClientSecret));

    private static string RenderConsentPage(
        string clientId, string redirectUri, string state, string institution, IReadOnlyList<MockAccount> accounts)
    {
        var e = HtmlEncoder.Default;
        var options = new StringBuilder();
        for (var i = 0; i < accounts.Count; i++)
        {
            var a = accounts[i];
            options.Append($"""
                <label><input type="radio" name="account_id" value="{e.Encode(a.Id)}" {(i == 0 ? "checked" : "")}>
                {e.Encode(a.AccountName)} <small>({e.Encode(a.AccountNumber)}{(a.IsJoint ? ", joint account" : "")})</small></label>
                """);
        }

        return $$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><title>Mock aggregator — link {{e.Encode(institution)}}</title>
            <style>body{font:15px system-ui;max-width:32rem;margin:3rem auto;padding:0 1rem}
            label{display:block;margin:.6rem 0}button{margin-right:.5rem;padding:.4rem 1rem}
            .note{color:#666;font-size:13px}</style></head>
            <body>
              <h1>Link your {{e.Encode(institution)}} account</h1>
              <p class="note">Development mock aggregator — no real bank is involved.</p>
              <form method="post" action="/oauth/authorize">
                <input type="hidden" name="client_id" value="{{e.Encode(clientId)}}">
                <input type="hidden" name="redirect_uri" value="{{e.Encode(redirectUri)}}">
                <input type="hidden" name="state" value="{{e.Encode(state)}}">
                {{options}}
                <p><button name="decision" value="allow">Allow</button><button name="decision" value="deny">Deny</button></p>
              </form>
            </body></html>
            """;
    }
}