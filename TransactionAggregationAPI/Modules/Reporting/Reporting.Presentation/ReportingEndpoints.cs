using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Reporting.Presentation.Endpoints;

namespace Modules.Reporting
{
    public static class ReportingEndpoints
    {
        // The report routes keep the paths they had when they were part of the Transactions module
        // (/transactions/summary, /transactions/aggregates/..., /customers/{id}/aggregates/...): clients are unchanged.
        public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
        {
            var transactions = app.MapApiGroup("transactions", "Reporting")
                                  .RequireAuthorization(AuthorizationPolicies.Staff);

            transactions.MapGetTransactionSummary();
            transactions.MapTransactionAggregates();

            app.MapApiGroup("customers", "Reporting")
               .RequireAuthorization(AuthorizationPolicies.Staff)
               .MapCustomerAggregates();

            return app;
        }
    }
}
