using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;

namespace BuildingBlocks.Web
{
    public static class CustomerOwnershipEndpointExtensions
    {
        /// <summary>
        /// Restricts the endpoint (or every endpoint of a group) to the customer named by
        /// the route's <paramref name="routeParameter"/>. Anyone else gets 404, not 403, so
        /// the response doesn't confirm that another customer's id exists.
        /// </summary>
        public static TBuilder RequireCustomerOwnership<TBuilder>(this TBuilder builder, string routeParameter = "customerId")
            where TBuilder : IEndpointConventionBuilder =>
            builder.AddEndpointFilter(new CustomerOwnershipFilter(routeParameter));
    }

    internal sealed class CustomerOwnershipFilter(string routeParameter) : IEndpointFilter
    {
        public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var userContext = httpContext.RequestServices.GetRequiredService<IUserContext>();

            if (!Guid.TryParse(httpContext.GetRouteValue(routeParameter)?.ToString(), out var customerId))
                return ValueTask.FromResult<object?>(Results.NotFound());

            // Same ProblemDetails as a customer that doesn't exist (Customers' CustomerErrors.NotFound),
            // so every 404 has one shape; the route tests pin the two together.
            return customerId == userContext.UserId
                ? next(context)
                : ValueTask.FromResult<object?>(
                    CustomResults.Problem(Result.Failure(Error.NotFound("Customer", customerId))));
        }
    }
}