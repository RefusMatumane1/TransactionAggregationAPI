using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Commands.CategorizeTransaction;
using TransactionAggregation.Application.Commands.CreateTransaction;
using TransactionAggregation.Application.Queries.Transaction.GetTransaction;
using TransactionAggregation.Domain.Enums;
using TransactionAggregationAPI.DTOs;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints
{
    public static class TransactionEndpoints
    {
        public static void MapTransactionEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v{version:apiVersion}/transactions")
                         .WithApiVersionSet()
                         .WithTags("Transactions")
                         .RequireRateLimiting("FixedWindow");

            group.MapGet("/{id:guid}", GetTransactionById)
                       .WithName("GetTransactionById")
                       .WithSummary("Get a specific transaction by ID")
                       .Produces<TransactionResponse>(StatusCodes.Status200OK)
                       .Produces(StatusCodes.Status404NotFound)
                       .Produces(StatusCodes.Status429TooManyRequests)
                       .WithOpenApi();

            // POST endpoints
            group.MapPost("/", CreateTransaction)
                .WithName("CreateTransaction")
                .WithSummary("Create a new transaction")
                .Accepts<CreateTransactionRequest>("application/json")
                .Produces<Guid>(StatusCodes.Status201Created)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status429TooManyRequests)
                .WithOpenApi();

            // PATCH endpoints
            group.MapPatch("/{id:guid}/categorize", CategorizeTransaction)
                .WithName("CategorizeTransaction")
                .WithSummary("Categorize a transaction")
                .Accepts<CategorizeTransactionRequest>("application/json")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status429TooManyRequests)
                .WithOpenApi();

        }

        private static async Task<IResult> GetTransactionById(
             ISender sender,
             IMapper mapper,
             Guid id,
             CancellationToken cancellationToken)
        {
            var query = new GetTransactionQuery(id);
            var result = await sender.Send(query, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            var response = result.Value.Adapt<TransactionResponse>(mapper.Config);
            return Results.Ok(response);
        }

        private static async Task<IResult> CreateTransaction(
        ISender sender,
        IMapper mapper,
        [FromBody] CreateTransactionRequest request,
        Guid customerId,
        CancellationToken cancellationToken)
        {
            var command = new CreateTransactionCommand(
                customerId,
                request.Amount,
                request.Currency,
                request.TransactionDate,
                request.Description,
                request.SourceSystem);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.Created($"/api/v1/transactions/{result.Value}", result.Value);
        }

        private static async Task<IResult> CategorizeTransaction(
            ISender sender,
            Guid id,
            [FromBody] CategorizeTransactionRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CategorizeTransactionCommand(id, request.Category);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.NoContent();
        }
    }

    public sealed record PaginationQueryParams(
    DateTime? StartDate,
    DateTime? EndDate,
    TransactionCategory? Category,
    int Page = 1,
    int PageSize = 20);
}
