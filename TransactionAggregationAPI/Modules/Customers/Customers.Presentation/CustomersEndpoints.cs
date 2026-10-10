using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Endpoints;

namespace Modules.Customers
{
    public static class CustomersEndpoints
    {
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