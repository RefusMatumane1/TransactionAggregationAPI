using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Commands.CreateTransaction;
using TransactionAggregation.Application.Commands.Customer.CreateCustomer;
using TransactionAggregation.Application.Commands.Customer.DeleteCustomer;
using TransactionAggregation.Application.Commands.Customer.Login;
using TransactionAggregation.Application.Commands.Customer.UpdateCustomer;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Application.Features.Transactions.Commands.SyncTransactions;
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

        group.MapGet("/", GetAllCustomers)
             .WithName("GetAllCustomers")
             .WithSummary("Get all customers with pagination")
             .Produces<PagedResponse<CustomerResponse>>(StatusCodes.Status200OK)
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

        // DELETE endpoints
        group.MapDelete("/{customerId:guid}", DeleteCustomer)
             .WithName("DeleteCustomer")
             .WithSummary("Delete a customer")
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests);


        group.MapPost("/login", Login)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous();

        // Transaction sub-resources
        group.MapPost("/{customerId:guid}/transactions", CreateTransaction)
             .WithName("CreateTransactionForCustomer")
             .WithSummary("Create a new transaction for a customer")
             .Accepts<CreateTransactionRequest>("application/json")
             .Produces<Guid>(StatusCodes.Status201Created)
             .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/{customerId:guid}/transactions/sync", SyncTransactions)
             .WithName("SyncCustomerTransactions")
             .WithSummary("Aggregate and sync transactions from all external sources for a customer")
             .Produces<SyncTransactionsResult>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests);

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

    private static async Task<IResult> GetAllCustomers(
        ISender sender,
        IMapper mapper,
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllCustomersQuery(page, pageSize, searchTerm);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        var mappedItems = result.Value.Items.Adapt<IEnumerable<CustomerResponse>>(mapper.Config);
        var pagedResult = new PagedResult<CustomerResponse>(mappedItems, result.Value.TotalCount, result.Value.CurrentPage, result.Value.PageSize);
        var response = PagedResponse<CustomerResponse>.From(pagedResult);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetCustomerByEmail(
        ISender sender,
        IMapper mapper,
        string email,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByEmailQuery(email);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

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

    private static async Task<IResult> DeleteCustomer(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new DeleteCustomerCommand(customerId);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }

    private static async Task<IResult> Login(
        ISender sender,
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var command = new LoginUserCommand(request.Username, request.Password);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Ok(new { Token = result.Value });
    }

    private static async Task<IResult> CreateTransaction(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new CreateTransactionCommand(
            customerId,
            request.Amount,
            request.Currency,
            request.TransactionDate,
            request.Description,
            request.SourceSystem,
            request.AccountId);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Created($"/api/v1/transactions/{result.Value}", result.Value);
    }

    private static async Task<IResult> SyncTransactions(
        ISender sender,
        Guid customerId,
        IUserContext userContext,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (customerId != userContext.UserId)
            return Results.NotFound();
        var command = new SyncTransactionsCommand
        {
            CustomerId = customerId,
            IdempotencyKey = idempotencyKey
        };

        var result = await sender.Send(command, cancellationToken);

        //if (result.IsFailure)
           // return CustomResults.Problem(result);

        return Results.Ok();
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