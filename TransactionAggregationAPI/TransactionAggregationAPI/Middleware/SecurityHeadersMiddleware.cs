namespace TransactionAggregationAPI.Middleware
{
    internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
    {
        public Task Invoke(HttpContext context)
        {
            var isApi = context.Request.Path.StartsWithSegments("/api");

            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";

                if (isApi)
                {
                    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            return next(context);
        }
    }
}