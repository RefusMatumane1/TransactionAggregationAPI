using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.BankLink.CompleteBankLink;
using TransactionAggregation.Application.Commands.BankLink.InitiateBankLink;
using TransactionAggregation.Application.Commands.BankLink.RevokeBankLink;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Queries.BankLink.GetBankLinks;
using TransactionAggregationAPI.DTOs.BankLink;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints;

public static class BankLinkEndpoints
{
    public static WebApplication MapBankLinkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/customers/{customerId:guid}/bank-links")
                      .WithApiVersionSet()
                      .WithTags("BankLinks")
                      .RequireRateLimiting("FixedWindow")
                      .RequireAuthorization();

        group.MapPost("/", InitiateBankLink)
            .WithName("InitiateBankLink")
            .WithSummary("Start the consent flow to link a South African bank account via the account aggregator")
            .Produces<InitiateBankLinkResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/", GetBankLinks)
            .WithName("GetBankLinks")
            .WithSummary("List the customer's linked bank accounts and their status")
            .Produces<IReadOnlyList<BankLinkDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{bankLinkId:guid}", RevokeBankLink)
            .WithName("RevokeBankLink")
            .WithSummary("Revoke a linked bank account")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        // Not under the customer-scoped/authenticated group above: this is the aggregator
        // redirecting the customer's own browser back to us, so it carries no JWT — the
        // one-time `state` value (see InitiateBankLinkCommandHandler) is what proves which
        // customer/institution this belongs to, not the caller's identity.
        var callbackGroup = app.MapGroup("/api/v{version:apiVersion}/bank-links")
            .WithApiVersionSet()
            .WithTags("BankLinks");

        callbackGroup.MapGet("/callback", CompleteBankLink)
            .WithName("CompleteBankLink")
            .WithSummary("OAuth redirect target the aggregator sends the customer's browser back to")
            .RequireRateLimiting("FixedWindow")
            .AllowAnonymous()
            .Produces<CompleteBankLinkResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> InitiateBankLink(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        [FromBody] InitiateBankLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var command = new InitiateBankLinkCommand(customerId, request.Institution);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Ok(new InitiateBankLinkResponse(result.Value));
    }

    private static async Task<IResult> GetBankLinks(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var query = new GetBankLinksQuery(customerId);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Ok(result.Value);
    }

    private static async Task<IResult> RevokeBankLink(
        ISender sender,
        Guid customerId,
        Guid bankLinkId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var command = new RevokeBankLinkCommand(customerId, bankLinkId);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }

    private static async Task<IResult> CompleteBankLink(
        ISender sender,
        string code,
        string state,
        CancellationToken cancellationToken)
    {
        var command = new CompleteBankLinkCommand(code, state);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Ok(new CompleteBankLinkResponse(result.Value));
    }
}

public sealed record InitiateBankLinkResponse(string AuthorizationUrl);
public sealed record CompleteBankLinkResponse(Guid AccountId);
