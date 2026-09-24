using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Web
{
    public static class ApiRouteGroups
    {
        /// <summary>
        /// A route group under <c>/api/v{version}/{prefix}</c> with the conventions every
        /// module API shares: API versioning, the OpenAPI tag, and the fixed-window rate
        /// limit (with its 429 documented once here rather than on every endpoint).
        /// Authorization is left to the caller — it differs per group.
        /// </summary>
        public static IVersionedEndpointRouteBuilder MapApiGroup(this IEndpointRouteBuilder app, string prefix, string tag) =>
            app.MapGroup($"/api/v{{version:apiVersion}}/{prefix.TrimStart('/')}")
               .WithApiVersionSet()
               .WithTags(tag)
               .RequireRateLimiting(RateLimitPolicies.FixedWindow)
               .WithMetadata(new ProducesResponseTypeAttribute(StatusCodes.Status429TooManyRequests));
    }
}