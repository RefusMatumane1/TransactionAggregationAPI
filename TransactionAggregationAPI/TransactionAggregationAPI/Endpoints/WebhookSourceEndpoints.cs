using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Features.WebhookSources.Commands.ActivateWebhookSource;
using TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource;
using TransactionAggregation.Application.Features.WebhookSources.Commands.DeactivateWebhookSource;
using TransactionAggregation.Application.Features.WebhookSources.Commands.RotateWebhookSourceKey;
using TransactionAggregation.Application.Features.WebhookSources.Queries.GetWebhookSources;
using TransactionAggregationAPI.DTOs.WebhookSources;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints;

/// <summary>Admin-only management of WebhookSource rows — who's allowed to push transactions
/// via /api/v1/webhooks/bank-aggregator/transactions and with what key. Every route requires
/// the "Admin" policy (the Keycloak "admin" realm role) — see Program.cs.</summary>
public static class WebhookSourceEndpoints
{
    public static WebApplication MapWebhookSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/admin/webhook-sources")
                      .WithApiVersionSet()
                      .WithTags("Admin")
                      .RequireRateLimiting("FixedWindow")
                      .RequireAuthorization("Admin");

        group.MapGet("/", GetWebhookSources)
            .WithName("GetWebhookSources")
            .WithSummary("List all webhook sources (never includes key material)")
            .Produces<IReadOnlyList<WebhookSourceResponse>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateWebhookSource)
            .WithName("CreateWebhookSource")
            .WithSummary("Register a new webhook source — the returned API key is shown exactly once")
            .Produces<CreateWebhookSourceResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/rotate", RotateWebhookSourceKey)
            .WithName("RotateWebhookSourceKey")
            .WithSummary("Replace a source's key — the old one stops working immediately; the new one is shown exactly once")
            .Produces<RotateWebhookSourceKeyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", ActivateWebhookSource)
            .WithName("ActivateWebhookSource")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deactivate", DeactivateWebhookSource)
            .WithName("DeactivateWebhookSource")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetWebhookSources(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetWebhookSourcesQuery(), cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var response = result.Value
            .Select(s => new WebhookSourceResponse(s.Id, s.Name, s.IsActive, s.CreatedAt, s.LastUsedAt))
            .ToList();

        return Results.Ok(response);
    }

    private static async Task<IResult> CreateWebhookSource(
        ISender sender,
        [FromBody] CreateWebhookSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateWebhookSourceCommand(request.Name), cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var response = new CreateWebhookSourceResponse(result.Value.Id, result.Value.Name, result.Value.ApiKey);
        return Results.Created($"/api/v1/admin/webhook-sources/{response.Id}", response);
    }

    private static async Task<IResult> RotateWebhookSourceKey(
        ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RotateWebhookSourceKeyCommand(id), cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Ok(new RotateWebhookSourceKeyResponse(result.Value));
    }

    private static async Task<IResult> ActivateWebhookSource(
        ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ActivateWebhookSourceCommand(id), cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }

    private static async Task<IResult> DeactivateWebhookSource(
        ISender sender, Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeactivateWebhookSourceCommand(id), cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }
}

public sealed record WebhookSourceResponse(Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt);
public sealed record CreateWebhookSourceResponse(Guid Id, string Name, string ApiKey);
public sealed record RotateWebhookSourceKeyResponse(string ApiKey);
