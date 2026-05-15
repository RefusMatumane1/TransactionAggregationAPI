using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.CategorizeTransaction;
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
    }

    public sealed record PaginationQueryParams(
        DateTime? StartDate,
        DateTime? EndDate,
        TransactionCategory? Category,
        int Page = 1,
        int PageSize = 20);
}
