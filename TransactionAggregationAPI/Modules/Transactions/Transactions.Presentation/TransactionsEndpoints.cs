using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Endpoints.CustomerTransactions;
using Modules.Transactions.Presentation.Endpoints.Transactions;
using Modules.Transactions.Presentation.Endpoints.Webhooks;

namespace Modules.Transactions
{
    public static class TransactionsEndpoints
    {
        public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder app)
        {
            // Served under /customers/{customerId} because they're scoped to one customer's
            // data, but they're Transactions queries, so they live in this module.
            var customerTransactions = app.MapApiGroup("customers/{customerId:guid}/transactions", "Customers")
                                          .RequireAuthorization()
                                          .RequireCustomerOwnership();

            customerTransactions.MapGetCustomerWithTransactions();
            customerTransactions.MapFilterCustomerTransactions();
            customerTransactions.MapGetCustomerTransactionSummary();
            customerTransactions.MapExportCustomerTransactions();

            var transactions = app.MapApiGroup("transactions", "Transactions")
                                  .RequireAuthorization();

            transactions.MapGetTransactionById();
            transactions.MapCategorizeTransaction();

            // Machine-to-machine: authenticated per endpoint by API key, not by bearer token.
            var webhooks = app.MapApiGroup("webhooks", "Webhooks")
                              .AllowAnonymous();

            webhooks.MapReceiveBankTransactions();

            return app;
        }
    }
}