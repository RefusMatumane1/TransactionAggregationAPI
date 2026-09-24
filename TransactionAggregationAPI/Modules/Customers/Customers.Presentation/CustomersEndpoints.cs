using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Endpoints.Accounts;
using Modules.Customers.Presentation.Endpoints.Customers;

namespace Modules.Customers
{
    public static class CustomersEndpoints
    {
        public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
        {
            var customers = app.MapApiGroup("customers", "Customers")
                               .RequireAuthorization();

            customers.MapCreateCustomer();
            customers.MapGetCustomerByEmail();

            // Everything addressed by /customers/{customerId} is visible to that customer only.
            var customer = app.MapApiGroup("customers/{customerId:guid}", "Customers")
                              .RequireAuthorization()
                              .RequireCustomerOwnership();

            customer.MapGetCustomerById();
            customer.MapUpdateCustomer();

            var accounts = app.MapApiGroup("customers/{customerId:guid}/accounts", "Accounts")
                              .RequireAuthorization()
                              .RequireCustomerOwnership();

            accounts.MapGetCustomerAccounts();
            accounts.MapGetAccountById();
            accounts.MapCreateAccount();
            accounts.MapDeactivateAccount();

            return app;
        }
    }
}