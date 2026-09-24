using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Text.RegularExpressions;

namespace TransactionAggregationAPI.Extensions;

/// <summary>
/// Adds the JWT Bearer scheme to the OpenAPI document, and a security requirement only to the
/// operations that need it: anonymous endpoints and the API-key webhook are left without one.
/// </summary>
public sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == SchemeId))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Obtain a token via the Keycloak realm's OIDC flow (see README.md) and supply it as `Authorization: Bearer <token>`."
        };

        var securityRequirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = []
        };

        foreach (var group in context.DescriptionGroups)
        {
            foreach (var apiDescription in group.Items)
            {
                if (!RequiresBearerAuth(apiDescription))
                    continue;

                var path = NormalizeRoutePath(apiDescription.RelativePath);
                if (!document.Paths.TryGetValue(path, out var pathItem))
                    continue;

                var httpMethod = HttpMethodFromApiDescription(apiDescription.HttpMethod);
                if (pathItem.Operations?.TryGetValue(httpMethod, out var operation) is true)
                    operation.Security = [securityRequirement];
            }
        }
    }

    private static bool RequiresBearerAuth(Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription apiDescription)
    {
        var endpointMetadata = apiDescription.ActionDescriptor.EndpointMetadata;
        if (endpointMetadata is null)
            return false;

        var hasAuthorizeData = endpointMetadata.OfType<IAuthorizeData>().Any();
        var allowsAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();

        // The webhook endpoint authenticates with WebhookApiKeyEndpointFilter and carries no
        // IAuthorizeData, so the check above already excludes it.
        return hasAuthorizeData && !allowsAnonymous;
    }

    private static readonly Regex RouteConstraintSuffix = new(@"\{([^:}]+):[^}]+\}", RegexOptions.Compiled);

    /// <summary>
    /// ApiDescription.RelativePath has no leading slash, can carry a
    /// "?param=value" suffix for [FromQuery]-bound endpoints, keeps route
    /// constraints like "{customerId:guid}" verbatim from the C# route
    /// template, and — for a group-root endpoint mapped as MapGet("/", ...) —
    /// keeps the group prefix's own trailing slash (".../accounts/"). None of
    /// that survives into the OpenAPI document's own Paths keys ("{customerId}",
    /// no trailing slash). Without normalizing all of it, the lookup below
    /// misses for most real endpoints and silently leaves the operation with no
    /// security requirement at all — the exact failure this transformer exists
    /// to prevent.
    /// </summary>
    private static string NormalizeRoutePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return "/";

        var path = relativePath;
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        path = RouteConstraintSuffix.Replace(path, "{$1}");

        if (!path.StartsWith('/'))
            path = "/" + path;

        if (path.Length > 1 && path.EndsWith('/'))
            path = path[..^1];

        return path;
    }

    private static HttpMethod HttpMethodFromApiDescription(string? httpMethod) => httpMethod?.ToUpperInvariant() switch
    {
        "GET" => HttpMethod.Get,
        "POST" => HttpMethod.Post,
        "PUT" => HttpMethod.Put,
        "PATCH" => HttpMethod.Patch,
        "DELETE" => HttpMethod.Delete,
        _ => throw new NotSupportedException($"Unsupported HTTP method '{httpMethod}' encountered while building OpenAPI security requirements.")
    };
}