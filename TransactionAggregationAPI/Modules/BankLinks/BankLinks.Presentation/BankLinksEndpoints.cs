using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.BankLinks.Presentation.Endpoints;

namespace Modules.BankLinks
{
    public static class BankLinksEndpoints
    {
        public static IEndpointRouteBuilder MapBankLinksEndpoints(this IEndpointRouteBuilder app)
        {
            var customerBankLinks = app.MapApiGroup("customers/{customerId:guid}/bank-links", "BankLinks")
                                       .RequireAuthorization()
                                       .RequireCustomerOwnership();

            customerBankLinks.MapInitiateBankLink();
            customerBankLinks.MapGetBankLinks();

            // The aggregator redirects the customer's browser here, so it can't carry our bearer token.
            var callback = app.MapApiGroup("bank-links", "BankLinks")
                              .AllowAnonymous();

            callback.MapCompleteBankLink();

            return app;
        }
    }
}