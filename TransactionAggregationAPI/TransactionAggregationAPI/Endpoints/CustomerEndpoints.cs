using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TransactionAggregation.Application.Commands.Customer.CreateCustomer;
using TransactionAggregation.Application.Commands.Customer.DeleteCustomer;
using TransactionAggregation.Application.Commands.Customer.UpdateCustomer;
using TransactionAggregation.Application.Queries.Customer.GetCustomer;
using TransactionAggregationAPI.DTOs;
using TransactionAggregationAPI.DTOs.Customer;
using TransactionAggregationAPI.Endpoints;
using TransactionAggregationAPI.Infrastructure;

namespace TransactionAggregation.API.Endpoints;

public static class CustomerEndpoints
{
    public static WebApplication MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/customers")
                      .WithApiVersionSet()
                      .WithTags("Customers")
                      .RequireRateLimiting("FixedWindow");

        // GET endpoints
        group.MapGet("/{customerId:guid}", GetCustomerById)
             .WithName("GetCustomerById")
             .WithSummary("Get a specific customer by ID")
             .Produces<CustomerResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        group.MapGet("/{customerId:guid}/transactions", GetCustomerWithTransactions)
             .WithName("GetCustomerWithTransactions")
             .WithSummary("Get customer with their transactions")
             .Produces<CustomerWithTransactionsResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        group.MapGet("/", GetAllCustomers)
             .WithName("GetAllCustomers")
             .WithSummary("Get all customers with pagination")
             .Produces<PagedResponse<CustomerResponse>>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        group.MapGet("/email/{email}", GetCustomerByEmail)
             .WithName("GetCustomerByEmail")
             .WithSummary("Get a customer by email address")
             .Produces<CustomerResponse>(StatusCodes.Status200OK)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        // POST endpoints
        group.MapPost("/", CreateCustomer)
             .WithName("CreateCustomer")
             .WithSummary("Create a new customer")
             .Accepts<CreateCustomerRequest>("application/json")
             .Produces<Guid>(StatusCodes.Status201Created)
             .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status409Conflict)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        // PUT endpoints
        group.MapPut("/{customerId:guid}", UpdateCustomer)
             .WithName("UpdateCustomer")
             .WithSummary("Update an existing customer")
             .Accepts<UpdateCustomerRequest>("application/json")
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        // DELETE endpoints
        group.MapDelete("/{customerId:guid}", DeleteCustomer)
             .WithName("DeleteCustomer")
             .WithSummary("Delete a customer")
             .Produces(StatusCodes.Status204NoContent)
             .Produces(StatusCodes.Status404NotFound)
             .Produces(StatusCodes.Status400BadRequest)
             .Produces(StatusCodes.Status429TooManyRequests)
             .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetCustomerById(
        ISender sender,
        IMapper mapper,
        Guid customerId,
        CancellationToken cancellationToken)
    {
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
        [AsParameters] PaginationQueryParams pagination,
        CancellationToken cancellationToken)
    {
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
        //var response = PagedResponse<CustomerResponse>.From(result.Value with { Items = mappedItems });

        return Results.Ok(mappedItems);
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
            request.Name);

        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.Created($"/api/v1/customers/{result.Value}", result.Value);
    }

    private static async Task<IResult> UpdateCustomer(
        ISender sender,
        Guid customerId,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
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
        CancellationToken cancellationToken)
    {
        var command = new DeleteCustomerCommand(customerId);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
            return CustomResults.Problem(result);

        return Results.NoContent();
    }
}