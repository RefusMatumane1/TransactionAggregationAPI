using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.Customer.CreateCustomer;
using TransactionAggregation.Application.Commands.Customer.UpdateCustomer;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.Queries.ExportTransactions;
using TransactionAggregation.Application.Features.Transactions.Queries.GetTransactions;
using TransactionAggregation.Application.Queries.Customer;
using TransactionAggregation.Application.Queries.Customer.GetCustomer;
using TransactionAggregation.Domain.Enums;
using TransactionAggregationAPI.DTOs;
using TransactionAggregationAPI.DTOs.Customer;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregationAPI.Endpoints;

public static class CustomerEndpoints
{
    public static WebApplication MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/customers")
                      .WithApiVersionSet()
                      .WithTags("Customers")
                      .RequireRateLimiting("FixedWindow")
                      .RequireAuthorization();

        // GET endpoints
        group.MapGet("/{customerId:guid}", GetCustomerById)
            .WithName("GetCustomerById")
            .WithSummary("Get a specific customer by ID")
            .Produces<CustomerResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{customerId:guid}/transactions", GetCustomerWithTransactions)
            .WithName("GetCustomerWithTransactions")
            .WithSummary("Get customer with their transactions")
            .Produces<CustomerWithTransactionsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/email/{email}", GetCustomerByEmail)
             .WithName("GetCustomerByEmail")
             .WithSummary("Get a customer by email address")
             .Produces<CustomerResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

        // POST endpoints
        group.MapPost("/", CreateCustomer)
             .WithName("CreateCustomer")
             .WithSummary("Create a new customer")
             .Accepts<CreateCustomerRequest>("application/json")
             .Produces<Guid>(StatusCodes.Status201Created)
             .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status409Conflict)
             .Produces(StatusCodes.Status429TooManyRequests)
             .AllowAnonymous();

        // PUT endpoints
        group.MapPut("/{customerId:guid}", UpdateCustomer)
             .WithName("UpdateCustomer")
             .WithSummary("Update an existing customer")
             .Accepts<UpdateCustomerRequest>("application/json")
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests);

        // Transaction sub-resources
        group.MapGet("/{customerId:guid}/transactions/filter", FilterTransactions)
             .WithName("FilterCustomerTransactions")
             .WithSummary("Get paginated transactions with rich filtering: date range, category, status, amount, source, search")
             .Produces<PaginatedResponse<TransactionAggregation.Application.Features.Transactions.DTOs.TransactionDto>>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{customerId:guid}/transactions/summary", GetTransactionSummary)
             .WithName("GetCustomerTransactionSummary")
             .WithSummary("Get spending summary: total income/expenses, spend per category, and monthly breakdowns")
             .Produces<TransactionAggregation.Application.Common.DTOs.TransactionSummaryDto>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{customerId:guid}/transactions/export", ExportTransactions)
             .WithName("ExportCustomerTransactions")
             .WithSummary("Export transactions to CSV with optional date range and category filter")
             .Produces(StatusCodes.Status200OK, contentType: "text/csv")
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

        return app;
    }

    private static async Task<IResult> GetCustomerById(
        ISender sender,
        IMapper mapper,
        Guid customerId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();

        var query = new GetCustomerQuery(customerId);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var response = result.Value.Adapt<CustomerResponse>(mapper.Config);
        return Results.Ok(response);
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

    private static async Task<IResult> GetCustomerByEmail(
        ISender sender,
        IMapper mapper,
        string email,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByEmailQuery(email);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        // A customer may only resolve their own record by email — otherwise
        // this endpoint would let any authenticated user enumerate the customer table.
        if (result.Value.Id != userContext.UserId)
            return Results.NotFound();

        var response = result.Value.Adapt<CustomerResponse>(mapper.Config);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateCustomer(
        ISender sender,
        IMapper mapper,
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCustomerCommand(
            request.Email,
            request.Name,
            request.password);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Created($"/api/v1/customers/{result.Value}", result.Value);
    }

    private static async Task<IResult> UpdateCustomer(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new UpdateCustomerCommand(
            customerId,
            request.Email,
            request.Name);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
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
}