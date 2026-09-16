using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using TransactionAggregationAPI.DTOs.Webhooks;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints;

public static class WebhookEndpoints
{
    public static WebApplication MapWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/webhooks")
                      .WithApiVersionSet()
                      .WithTags("Webhooks")
                      .RequireRateLimiting("FixedWindow")

.AllowAnonymous();

        group.MapPost("/bank-aggregator/transactions", ReceiveBankTransactions)
             .WithName("ReceiveBankAggregatorTransactions")
             .WithSummary("Inbound webhook the account aggregator calls to push new transactions for a linked account")
             .AddEndpointFilter<ApiKeyEndpointFilter>()
             .Produces(StatusCodes.Status202Accepted)
             .Produces(StatusCodes.Status401Unauthorized)
             .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> ReceiveBankTransactions(
        ISender sender,
        HttpContext httpContext,
        [FromBody] BankTransactionsWebhookRequest request,
        CancellationToken cancellationToken)
    {

        var sourceName = (string)httpContext.Items[ApiKeyEndpointFilter.SourceNameItemKey]!;

        var transactions = request.Transactions
            .Select(t => new ExternalTransactionDTO
            {
                Id = t.Id,
                Amount = t.Amount,
                Currency = t.Currency,
                Description = t.Description,
                Category = t.Category ?? string.Empty,
                Date = t.Date
            })
            .ToList();

        var command = new ReceiveBankTransactionsCommand(sourceName, request.ExternalAccountId, transactions);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Accepted(value: new { InboxMessageId = result.Value });
    }
}