using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Web
{
    public static class CorrelationContext
    {
        public const string HeaderName = "X-Correlation-Id";

        private const string ItemKey = "CorrelationId";

        public static void Set(HttpContext context, string correlationId) => context.Items[ItemKey] = correlationId;

        public static string? Get(HttpContext context) => context.Items[ItemKey] as string;
    }
}