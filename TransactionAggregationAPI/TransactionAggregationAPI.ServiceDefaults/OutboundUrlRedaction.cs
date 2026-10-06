using System.Diagnostics;

namespace Microsoft.Extensions.Hosting
{
    // Marked requests are traced as scheme://host/[redacted]; pair with an HttpClient that has no request loggers.
    public static class OutboundUrlRedaction
    {
        private static readonly HttpRequestOptionsKey<bool> RedactKey = new("TransactionAggregation.RedactUrl");

        public static void Redact(HttpRequestMessage request) => request.Options.Set(RedactKey, true);

        public static void Enrich(Activity activity, HttpRequestMessage request)
        {
            if (!request.Options.TryGetValue(RedactKey, out var redact) || !redact || request.RequestUri is null)
                return;

            var redacted = $"{request.RequestUri.Scheme}://{request.RequestUri.Authority}/[redacted]";
            activity.SetTag("url.full", redacted);
            activity.SetTag("http.url", redacted);
            activity.DisplayName = $"{request.Method} {request.RequestUri.Authority}";
        }
    }
}