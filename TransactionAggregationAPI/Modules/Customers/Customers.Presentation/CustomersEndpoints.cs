using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Endpoints;

namespace Modules.Customers
{
    public static class CustomersEndpoints
    {
        // A customer's transactions and aggregates are served by the Transactions module under the
        // same /customers/{id} prefix; this module owns the customers and their account links.
        public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapApiGroup("customers", "Customers")
               .RequireAuthorization(AuthorizationPolicies.Staff)
               .MapCustomerReads();

            app.MapApiGroup("customers", "Customers")
               .RequireAuthorization(AuthorizationPolicies.Admin)
               .MapCustomerAdministration();

            return app;
        }
    }
}