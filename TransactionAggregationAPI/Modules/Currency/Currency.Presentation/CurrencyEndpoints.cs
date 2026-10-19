using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Currency.Presentation.Endpoints;

namespace Modules.Currency
{
    public static class CurrencyEndpoints
    {
        public static IEndpointRouteBuilder MapCurrencyEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapApiGroup("currencies", "Currencies")
               .RequireAuthorization(AuthorizationPolicies.Staff)
               .MapGetCurrencies();

            return app;
        }
    }
}
