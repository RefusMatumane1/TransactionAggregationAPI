using BuildingBlocks.Web;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Abstractions.Authentication;
using Modules.Customers.Application.Features.CreateAccount;
using Modules.Customers.Application.Features.CreateCustomer;
using Modules.Customers.Application.Features.DeactivateAccount;
using Modules.Customers.Application.Features.GetAccountById;
using Modules.Customers.Application.Features.GetCustomer;
using Modules.Customers.Application.Features.GetCustomerAccounts;
using Modules.Customers.Application.Features.GetCustomerByEmail;
using Modules.Customers.Application.Features.UpdateCustomer;
using Modules.Customers.Infrastructure.Endpoints;

namespace Modules.Customers
{
    public static class CustomersEndpoints
    {
        public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
        {
            MapCustomerRoutes(app);
            MapAccountRoutes(app);
            return app;
        }

        private static void MapCustomerRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/v{version:apiVersion}/customers")
                          .WithApiVersionSet()
                          .WithTags("Customers")
                          .RequireRateLimiting("FixedWindow")
                          .RequireAuthorization();

            group.MapGet("/{customerId:guid}", GetCustomerById)
                        .WithName("GetCustomerById")
                        .WithSummary("Get a specific customer by ID")
                        .Produces<CustomerResponse>(StatusCodes.Status200OK)
                        .Produces(StatusCodes.Status404NotFound)
                        .Produces(StatusCodes.Status429TooManyRequests);

            group.MapGet("/email/{email}", GetCustomerByEmail)
                 .WithName("GetCustomerByEmail")
                 .WithSummary("Get a customer by email address")
                 .Produces<CustomerResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status429TooManyRequests);

            group.MapPost("/", CreateCustomer)
                         .WithName("CreateCustomer")
                         .WithSummary("Create a new customer")
                         .Accepts<CreateCustomerRequest>("application/json")
                         .Produces<Guid>(StatusCodes.Status201Created)
                         .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                         .Produces(StatusCodes.Status409Conflict)
                         .Produces(StatusCodes.Status429TooManyRequests)
                         .AllowAnonymous();

            group.MapPut("/{customerId:guid}", UpdateCustomer)
                         .WithName("UpdateCustomer")
                         .WithSummary("Update an existing customer")
                         .Accepts<UpdateCustomerRequest>("application/json")
                         .Produces(StatusCodes.Status204NoContent)
                         .Produces(StatusCodes.Status404NotFound)
                         .Produces(StatusCodes.Status400BadRequest)
                         .Produces(StatusCodes.Status429TooManyRequests);
        }

        private static void MapAccountRoutes(IEndpointRouteBuilder app)
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

            if (result.Value.CustomerId != customerId)
                return Results.NotFound();

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

            var ownershipQuery = new GetAccountByIdQuery(accountId);
            var ownershipResult = await sender.Send(ownershipQuery, cancellationToken);

            if (ownershipResult.IsFailure)
                return CustomResults.Problem(ownershipResult);

            if (ownershipResult.Value.CustomerId != customerId)
                return Results.NotFound();

            var command = new DeactivateAccountCommand(accountId);
            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
                return CustomResults.Problem(result);

            return Results.NoContent();
        }
    }
}
