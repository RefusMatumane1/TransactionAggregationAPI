using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TransactionAggregationUI;
using TransactionAggregationUI.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
if (string.IsNullOrEmpty(apiBaseUrl))
    apiBaseUrl = builder.HostEnvironment.BaseAddress;

// Keycloak-backed OIDC login (Authorization Code + PKCE) — see wwwroot/appsettings*.json's
// "Keycloak" section for Authority/ClientId. This registers AuthenticationStateProvider,
// AddAuthorizationCore, and AuthorizationMessageHandler for us.
builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");

    // Matches the "roles" claim the transaction-ui client's realm-role protocol mapper adds
    // to the token (see keycloak/realm-export.json) — lets AuthorizeView Roles="admin" /
    // [Authorize(Roles = "admin")] work client-side the same way RequireRole("admin") does
    // on the API (see Program.cs there).
    options.UserOptions.RoleClaim = "roles";
});

// AuthorizationMessageHandler attaches the signed-in user's access token to every request this
// client makes — only to apiBaseUrl, never to Keycloak itself or any other origin.
builder.Services.AddHttpClient("api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: [apiBaseUrl]));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<WebhookSourceService>();

await builder.Build().RunAsync();
