using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Endpoints.Customers;
using Modules.Transactions.Presentation.Endpoints.Reporting;
using Modules.Transactions.Presentation.Endpoints.Transactions;
using Modules.Transactions.Presentation.Endpoints.Webhooks;

namespace Modules.Transactions
{
    public static class TransactionsEndpoints
    {
        public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder app)
        {
            var transactions = app.MapApiGroup("transactions", "Transactions")
                                  .RequireAuthorization(AuthorizationPolicies.Staff);

            transactions.MapListTransactions();
            transactions.MapGetTransactionById();
            transactions.MapGetTransactionSummary();
            transactions.MapTransactionAggregates();

            app.MapApiGroup("customers", "Customers")
               .RequireAuthorization(AuthorizationPolicies.Staff)
               .MapCustomerTransactions();

            var webhooks = app.MapApiGroup("webhooks", "Webhooks")
                              .AllowAnonymous();

            webhooks.MapReceiveBankTransactions();

            return app;
        }
    }
}