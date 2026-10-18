using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.Reporting
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