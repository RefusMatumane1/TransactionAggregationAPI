using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TransactionAggregationUI;
using TransactionAggregationUI.Auth;
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

    options.UserOptions.RoleClaim = RolesClaimsPrincipalFactory.RoleClaim;
}).AddAccountClaimsPrincipalFactory<RolesClaimsPrincipalFactory>();

builder.Services.AddHttpClient(ApiClient.AuthorizedClient, client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler(sp => sp.GetRequiredService<AuthorizationMessageHandler>()
        .ConfigureHandler(authorizedUrls: [apiBaseUrl]));
builder.Services.AddHttpClient(ApiClient.AnonymousClient, client => client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<AggregateService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<CustomerService>();

await builder.Build().RunAsync();