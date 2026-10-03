using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Endpoints.Reporting;
using Modules.Transactions.Presentation.Endpoints.Transactions;
using Modules.Transactions.Presentation.Endpoints.Webhooks;

namespace Modules.Transactions
{
    public static class TransactionsEndpoints
    {
        public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder app)
        {
            // Read-only for staff and admins: a transaction is never changed through the API.
            var transactions = app.MapApiGroup("transactions", "Transactions")
                                  .RequireAuthorization(AuthorizationPolicies.Staff);

            transactions.MapListTransactions();
            transactions.MapGetTransactionById();
            transactions.MapGetTransactionSummary();
            transactions.MapTransactionAggregates();

            var webhooks = app.MapApiGroup("webhooks", "Webhooks")
                              .AllowAnonymous();

            webhooks.MapReceiveBankTransactions();

            return app;
        }
    }
}