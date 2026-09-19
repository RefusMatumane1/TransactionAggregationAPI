using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TransactionAggregationAPI.Extensions;

/// <summary>
/// Instructions.md section 38 asks for OpenAPI documentation that makes the API
/// "understandable without reading the source code" — including how to
/// authenticate. AddOpenApi() alone doesn't add that: the generated spec had zero
/// security schemes defined, so a consumer reading only the docs (or using
/// Scalar's "Try it out") had no way to know a Bearer token was required, or how
/// to supply one. This wires the JWT Bearer scheme into the document and marks
/// only the endpoints that actually require it — customer registration
/// (AllowAnonymous), the bank-link OAuth callback, and the webhook ingestion
/// endpoint (API-key auth, a different scheme entirely) are correctly left
/// without it, rather than blanket-applying to every operation.
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

        // The webhook ingestion endpoint requires ApiKeyEndpointFilter, not JWT —
        // it carries no IAuthorizeData at all, so it's already excluded by the
        // hasAuthorizeData check without needing a special case here.
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
