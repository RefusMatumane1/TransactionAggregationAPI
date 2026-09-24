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

builder.Services.AddOidcAuthentication(options =>
{
    builder.Configuration.Bind("Keycloak", options.ProviderOptions);
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");

    options.UserOptions.RoleClaim = "roles";
});

builder.Services.AddHttpClient("api", client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: [apiBaseUrl]));

// For the few anonymous endpoints the app calls where a signed-in session isn't guaranteed
// (the bank-link callback, reached from the bank's consent page).
builder.Services.AddHttpClient("api-anonymous", client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<BankLinkService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<WebhookSourceService>();
builder.Services.AddScoped<AuditService>();

await builder.Build().RunAsync();