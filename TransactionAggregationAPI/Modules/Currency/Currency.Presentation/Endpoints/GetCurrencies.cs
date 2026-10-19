using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Currency.Contracts;
using Modules.Currency.Presentation.Responses;

namespace Modules.Currency.Presentation.Endpoints
{
    internal static class GetCurrencies
    {
        public static RouteHandlerBuilder MapGetCurrencies(this IEndpointRouteBuilder group) =>
            group.MapGet("/", Handle)
                 .WithName("GetCurrencies")
                 .WithSummary("The base currency and every supported currency with its rate to the base (the values currency, convertTo and deliveries accept)")
                 .Produces<CurrencyCatalogResponse>(StatusCodes.Status200OK);

        private static IResult Handle(ICurrencyCatalog catalog) => Results.Ok(CurrencyCatalogResponse.From(catalog));
    }
}