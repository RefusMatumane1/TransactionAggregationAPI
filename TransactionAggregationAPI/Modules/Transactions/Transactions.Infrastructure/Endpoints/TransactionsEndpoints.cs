using BuildingBlocks.Web;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Abstractions.Authentication;
using Modules.Transactions.Application.Commands.CategorizeTransaction;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Application.Common.Models;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetCustomerWithTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Application.Queries.Transaction.GetTransaction;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Endpoints;

namespace Modules.Transactions
{
    public static class TransactionsEndpoints
    {
        public static IEndpointRouteBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder app)
        {
            MapCustomerTransactionRoutes(app);
            MapTransactionRoutes(app);
            MapWebhookRoutes(app);
            return app;
        }

        // Served under /customers/{customerId} because they're scoped to one customer's
        // data, but they're Transactions queries, so they live in this module.
        private static void MapCustomerTransactionRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v{version:apiVersion}/customers")
                          .WithApiVersionSet()
                          .WithTags("Customers")
                          .RequireRateLimiting("FixedWindow")
                          .RequireAuthorization();

            group.MapGet("/{customerId:guid}/transactions", GetCustomerWithTransactions)
                .WithName("GetCustomerWithTransactions")
                .WithSummary("Get customer with their transactions")
                .Produces<CustomerWithTransactionsResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status429TooManyRequests);

            group.MapGet("/{customerId:guid}/transactions/filter", FilterTransactions)
                         .WithName("FilterCustomerTransactions")
                         .WithSummary("Get paginated transactions with rich filtering: date range, category, status, amount, source, search")
                         .Produces<PaginatedResponse<Modules.Transactions.Application.Features.Transactions.DTOs.TransactionDto>>(StatusCodes.Status200OK)
                         .Produces(StatusCodes.Status404NotFound)
                         .Produces(StatusCodes.Status429TooManyRequests);

            group.MapGet("/{customerId:guid}/transactions/summary", GetTransactionSummary)
                 .WithName("GetCustomerTransactionSummary")
                 .WithSummary("Get spending summary: total income/expenses, spend per category, and monthly breakdowns")
                 .Produces<TransactionSummaryDto>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status429TooManyRequests);

            group.MapGet("/{customerId:guid}/transactions/export", ExportTransactions)
                 .WithName("ExportCustomerTransactions")
                 .WithSummary("Export transactions to CSV with optional date range and category filter")
                 .Produces(StatusCodes.Status200OK, contentType: "text/csv")
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status429TooManyRequests);
        }

        private static void MapTransactionRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v{version:apiVersion}/transactions")
                         .WithApiVersionSet()
                         .WithTags("Transactions")
                         .RequireRateLimiting("FixedWindow")
                         .RequireAuthorization();

            group.MapGet("/{id:guid}", GetTransactionById)
                       .WithName("GetTransactionById")
                       .WithSummary("Get a specific transaction by ID")
                       .Produces<TransactionResponse>(StatusCodes.Status200OK)
                       .Produces(StatusCodes.Status404NotFound)
                       .Produces(StatusCodes.Status429TooManyRequests);

            group.MapPatch("/{id:guid}/categorize", CategorizeTransaction)
                .WithName("CategorizeTransaction")
                .WithSummary("Manually override the category of a transaction")
                .Accepts<CategorizeTransactionRequest>("application/json")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status429TooManyRequests);
        }

        private static void MapWebhookRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v{version:apiVersion}/webhooks")
                          .WithApiVersionSet()
                          .WithTags("Webhooks")
                          .RequireRateLimiting("FixedWindow")
                          .AllowAnonymous();

            group.MapPost("/bank-aggregator/transactions", ReceiveBankTransactions)
                 .WithName("ReceiveBankAggregatorTransactions")
                 .WithSummary("Inbound webhook the account aggregator calls to push new transactions for a linked account")
                 .AddEndpointFilter<WebhookApiKeyEndpointFilter>()
                 .Produces(StatusCodes.Status202Accepted)
                 .Produces(StatusCodes.Status401Unauthorized)
                 .Produces(StatusCodes.Status400BadRequest);
        }

        private static async Task<IResult> GetCustomerWithTransactions(
            ISender sender,
            IMapper mapper,
            Guid customerId,
            IUserContext userContext,
            [AsParameters] PaginationQueryParams pagination,
            CancellationToken cancellationToken)
        {
            if (customerId != userContext.UserId)
                return Results.NotFound();
            var query = new GetCustomerWithTransactionsQuery(
                customerId,
                pagination.StartDate,
                pagination.EndDate,
                pagination.Category,
                pagination.Page,
                pagination.PageSize);

            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            var response = new CustomerWithTransactionsResponse(
                Id: result.Value.Id,
                Email: result.Value.Email,
                Name: result.Value.Name,
                CreatedAt: result.Value.CreatedAt,
                UpdatedAt: result.Value.UpdatedAt,
                Transactions: result.Value.Transactions.Adapt<IEnumerable<TransactionResponse>>(mapper.Config),
                TotalTransactions: result.Value.TotalTransactions,
                TotalIncome: result.Value.TotalIncome,
                TotalExpenses: result.Value.TotalExpenses,
                NetBalance: result.Value.NetBalance
            );

            return Results.Ok(response);
        }

        private static async Task<IResult> FilterTransactions(
            ISender sender,
            Guid customerId,
            IUserContext userContext,
            int pageNumber = 1,
            int pageSize = 20,
            TransactionCategory? category = null,
            TransactionStatus? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            string? searchTerm = null,
            string? source = null,
            string? sortBy = null,
            bool sortDescending = true,
            CancellationToken cancellationToken = default)
        {
            if (customerId != userContext.UserId)
                return Results.NotFound();

            var query = new GetTransactionsQuery
            {
                CustomerId = customerId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Category = category,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                SearchTerm = searchTerm,
                Source = source,
                SortBy = sortBy,
                SortDescending = sortDescending
            };

            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.Ok(result.Value);
        }

        private static async Task<IResult> GetTransactionSummary(
            ISender sender,
            Guid customerId,
            IUserContext userContext,
            DateTime? startDate = null,
            DateTime? endDate = null,
            CancellationToken cancellationToken = default)
        {
            if (customerId != userContext.UserId)
                return Results.NotFound();
            var query = new GetTransactionSummaryQuery(
                customerId,
                startDate ?? DateTime.UtcNow.AddMonths(-12),
                endDate ?? DateTime.UtcNow);

            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.Ok(result.Value);
        }

        private static async Task<IResult> ExportTransactions(
            ISender sender,
            Guid customerId,
            IUserContext userContext,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            TransactionCategory? category = null,
            CancellationToken cancellationToken = default)
        {
            if (customerId != userContext.UserId)
                return Results.NotFound();
            var query = new ExportTransactionsQuery
            {
                CustomerId = customerId,
                FromDate = fromDate,
                ToDate = toDate,
                Category = category,
                Format = "csv"
            };

            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
        }

        private static async Task<IResult> GetTransactionById(
             ISender sender,
             IMapper mapper,
             Guid id,
             IUserContext userContext,
             CancellationToken cancellationToken)
        {
            var query = new GetTransactionQuery(id);
            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            if (result.Value.CustomerId != userContext.UserId)
                return Results.NotFound();

            var response = result.Value.Adapt<TransactionResponse>(mapper.Config);
            return Results.Ok(response);
        }

        private static async Task<IResult> CategorizeTransaction(
            ISender sender,
            Guid id,
            IUserContext userContext,
            [FromBody] CategorizeTransactionRequest request,
            CancellationToken cancellationToken)
        {
            var ownershipQuery = new GetTransactionQuery(id);
            var ownershipResult = await sender.Send(ownershipQuery, cancellationToken);

            if (ownershipResult.IsFailure)
                return CustomResults.Problem(ownershipResult);

            if (ownershipResult.Value.CustomerId != userContext.UserId)
                return Results.NotFound();

            var command = new CategorizeTransactionCommand(id, request.Category);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.NoContent();
        }

        private static async Task<IResult> ReceiveBankTransactions(
            ISender sender,
            HttpContext httpContext,
            [FromBody] BankTransactionsWebhookRequest request,
            CancellationToken cancellationToken)
        {
            var sourceName = (string)httpContext.Items[WebhookApiKeyEndpointFilter.SourceNameItemKey]!;

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
}
