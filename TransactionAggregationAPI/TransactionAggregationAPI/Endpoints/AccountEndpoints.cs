using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.Account.CreateAccount;
using TransactionAggregation.Application.Commands.Account.DeactivateAccount;
using TransactionAggregation.Application.Queries.Account.GetAccountById;
using TransactionAggregation.Application.Queries.Account.GetCustomerAccounts;
using TransactionAggregationAPI.DTOs.Account;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints;

public static class AccountEndpoints
{
    public static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/customers/{customerId:guid}/accounts")
                      .WithApiVersionSet()
                      .WithTags("Accounts")
                      .RequireRateLimiting("FixedWindow")
                      .RequireAuthorization();

        group.MapGet("/", GetCustomerAccounts)
            .WithName("GetCustomerAccounts")
            .WithSummary("Get all accounts for a customer")
            .Produces<IEnumerable<AccountResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{accountId:guid}", GetAccountById)
            .WithName("GetAccountById")
            .WithSummary("Get a specific account by ID")
            .Produces<AccountResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/", CreateAccount)
            .WithName("CreateAccount")
            .WithSummary("Create a new account for a customer")
            .Accepts<CreateAccountRequest>("application/json")
            .Produces<Guid>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPatch("/{accountId:guid}/deactivate", DeactivateAccount)
            .WithName("DeactivateAccount")
            .WithSummary("Deactivate an account")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> GetCustomerAccounts(
        ISender sender,
        IMapper mapper,
        Guid customerId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var query = new GetCustomerAccountsQuery(customerId);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var response = result.Value.Adapt<IEnumerable<AccountResponse>>(mapper.Config);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetAccountById(
        ISender sender,
        IMapper mapper,
        Guid customerId,
        Guid accountId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var query = new GetAccountByIdQuery(accountId);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var response = result.Value.Adapt<AccountResponse>(mapper.Config);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateAccount(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new CreateAccountCommand(
            customerId,
            request.AccountNumber,
            request.AccountName,
            request.AccountType,
            request.Currency);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Created(
            $"/api/v1/customers/{customerId}/accounts/{result.Value}",
            result.Value);
    }

    private static async Task<IResult> DeactivateAccount(
        ISender sender,
        Guid customerId,
        Guid accountId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new DeactivateAccountCommand(accountId);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }
}
