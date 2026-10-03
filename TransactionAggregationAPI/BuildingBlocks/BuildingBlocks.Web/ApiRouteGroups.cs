using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace BuildingBlocks.Web
{
    public static class ApiRouteGroups
    {
        public static IVersionedEndpointRouteBuilder MapApiGroup(this IEndpointRouteBuilder app, string prefix, string tag) =>
            app.MapGroup($"/api/v{{version:apiVersion}}/{prefix.TrimStart('/')}")
               .WithApiVersionSet()
               .WithTags(tag)
               .RequireRateLimiting(RateLimitPolicies.FixedWindow)
               .WithMetadata(new ProducesResponseTypeAttribute(StatusCodes.Status429TooManyRequests));
    }
}