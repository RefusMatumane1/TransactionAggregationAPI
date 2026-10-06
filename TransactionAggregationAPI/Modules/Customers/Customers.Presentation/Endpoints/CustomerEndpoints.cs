using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.GetCustomer;
using Modules.Customers.Application.Features.GetCustomers;
using Modules.Customers.Application.Features.UnlinkAccount;

namespace Modules.Customers.Presentation.Endpoints
{
    internal static class CustomerReadEndpoints
    {
        public static IEndpointRouteBuilder MapCustomerReads(this IEndpointRouteBuilder group)
        {
            group.MapGet("/", ListAsync)
                 .WithName("ListCustomers")
                 .WithSummary("Customers the caller can see (through an account at one of their banks), by name; search matches name or reference")
                 .Produces<CursorPagedResponse<CustomerSummaryResponse>>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}", GetAsync)
                 .WithName("GetCustomer")
                 .WithSummary("A customer and the linked bank accounts the caller may read")
                 .Produces<CustomerResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

            return group;
        }

        private static async Task<IResult> ListAsync(
            ISender sender, IUserContext user, string? search, string? cursor, int? pageSize, CancellationToken cancellationToken) =>
            (await sender.Send(new GetCustomersQuery(user.InstitutionAccess, search, cursor, pageSize ?? GetCustomersQuery.DefaultPageSize), cancellationToken))
                .ToOk(page => CursorPagedResponse<CustomerSummaryResponse>.From(page, CustomerSummaryResponse.From));

        private static async Task<IResult> GetAsync(ISender sender, IUserContext user, Guid customerId, CancellationToken cancellationToken) =>
            (await sender.Send(new GetCustomerQuery(customerId, user.InstitutionAccess), cancellationToken)).ToOk(CustomerResponse.From);
    }

    internal static class CustomerAdminEndpoints
    {
        public static IEndpointRouteBuilder MapCustomerAdministration(this IEndpointRouteBuilder group)
        {
            group.MapPost("/", CreateAsync)
                 .WithName("CreateCustomer")
                 .WithSummary("Register a customer under the organisation's own reference")
                 .Produces<CustomerResponse>(StatusCodes.Status201Created)
                 .Produces(StatusCodes.Status409Conflict)
                 .ProducesValidationProblem();

            group.MapPost("/{customerId:guid}/accounts", LinkAsync)
                 .WithName("LinkCustomerAccount")
                 .WithSummary("Link a bank account to the customer: its transactions (past and future) become part of the customer's view. Idempotent: 200 if it was already linked")
                 .Produces<CustomerResponse>(StatusCodes.Status201Created)
                 .Produces<CustomerResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapDelete("/{customerId:guid}/accounts", UnlinkAsync)
                 .WithName("UnlinkCustomerAccount")
                 .WithSummary("Remove a bank account from the customer's view; its transactions stay in the ledger")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            return group;
        }

        private static async Task<IResult> CreateAsync(ISender sender, [FromBody] CreateCustomerRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToCommand(), cancellationToken))
                .Match(created => Results.Created($"/api/v1/customers/{created.Id}", CustomerResponse.From(created)));

        private static async Task<IResult> LinkAsync(
            ISender sender, Guid customerId, [FromBody] LinkAccountRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToCommand(customerId), cancellationToken))
                .Match(result => result.Linked
                    ? Results.Created($"/api/v1/customers/{customerId}", CustomerResponse.From(result.Customer))
                    : Results.Ok(CustomerResponse.From(result.Customer)));

        private static async Task<IResult> UnlinkAsync(
            ISender sender, Guid customerId, string? institution, string? externalAccountId, CancellationToken cancellationToken) =>
            (await sender.Send(new UnlinkAccountCommand(customerId, institution?.Trim() ?? string.Empty, externalAccountId ?? string.Empty), cancellationToken))
                .ToNoContent();
    }
}