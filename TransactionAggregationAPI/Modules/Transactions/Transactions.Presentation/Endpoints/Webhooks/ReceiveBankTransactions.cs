using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Presentation.Responses;
using SharedKernel.Common.Enums;

namespace Modules.Transactions.Presentation.Endpoints.Webhooks
{
    internal static class ReceiveBankTransactions
    {
        private const string IdempotencyKeyHeader = "Idempotency-Key";

        public static RouteHandlerBuilder MapReceiveBankTransactions(this IEndpointRouteBuilder group) =>
            group.MapPost("/bank-aggregator/transactions", HandleAsync)
                 .WithName("ReceiveBankAggregatorTransactions")
                 .WithSummary("Inbound webhook the account aggregator calls to push new transactions for a linked account")
                 .AddEndpointFilter<WebhookApiKeyEndpointFilter>()
                 .Produces<WebhookReceiptResponse>(StatusCodes.Status202Accepted)
                 .Produces(StatusCodes.Status401Unauthorized)
                 .Produces(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status422UnprocessableEntity);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            IAuditTrail auditTrail,
            ILoggerFactory loggerFactory,
            HttpContext httpContext,
            [FromBody] BankTransactionsMessage request,
            [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            var sourceName = WebhookApiKeyEndpointFilter.GetSourceName(httpContext);
            var transactions = request.ToExternalTransactionDtos();
            var hasIdempotencyKey = !string.IsNullOrWhiteSpace(idempotencyKey);

            var command = new ReceiveBankTransactionsCommand(
                sourceName, request.ExternalAccountId, transactions,
                hasIdempotencyKey ? idempotencyKey : null,
                WebhookDeliveryAudit.Describe(httpContext, hasIdempotencyKey),
                request.EffectiveSchemaVersion);
            var result = await sender.Send(command, cancellationToken);

            // Validation failures never reach the handler, and a refused idempotency-key reuse
            // never reaches the inbox — neither is covered by the inbox's transactional audit,
            // so record the refusal here.
            if (result.IsFailure && result.Error.Type is ErrorType.Validation or ErrorType.Problem)
            {
                await WebhookDeliveryAudit.TryRecordAsync(
                    auditTrail, loggerFactory.CreateLogger(typeof(ReceiveBankTransactions)), httpContext,
                    AuditEventTypes.InboundRejected, sourceName, result.Error.Description,
                    request.ExternalAccountId,
                    new Dictionary<string, string> { ["transactionCount"] = transactions.Count.ToString() });
            }

            return result.Match(receipt => Results.Accepted(
                value: new WebhookReceiptResponse(receipt.InboxMessageId, receipt.IsDuplicate, receipt.Requeued)));
        }
    }
}