using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace TransactionAggregationUI.Services
{
    public sealed class ApiClient(IHttpClientFactory factory, ILogger<ApiClient> logger)
    {
        public const string AuthorizedClient = "api";

        public const string AnonymousClient = "api-anonymous";

        public async Task<(T? Value, string? Error)> GetAsync<T>(string url, bool anonymous = false, string? notFoundMessage = null)
        {
            var (response, error) = await TrySendAsync(HttpMethod.Get, url, body: null, anonymous, notFoundMessage);
            if (response is null)
                return (default, error);

            using (response)
                return (await response.Content.ReadFromJsonAsync<T>(), null);
        }

        public const int MaxPagesPerList = 20;

        // Capped, so a server fault can't turn into an endless loop in the browser.
        public async Task<List<T>> GetAllPagesAsync<T>(string url)
        {
            var items = new List<T>();
            string? cursor = null;
            for (var page = 0; page < MaxPagesPerList; page++)
            {
                var pageUrl = cursor is null ? url : $"{url}?cursor={Uri.EscapeDataString(cursor)}";
                var (result, _) = await GetAsync<Models.Shared.CursorPage<T>>(pageUrl);
                if (result is null)
                    break;

                items.AddRange(result.Items);
                cursor = result.NextCursor;
                if (!result.HasMore || cursor is null)
                    break;
            }
            return items;
        }

        public async Task<(T? Value, string? Error)> SendAsync<T>(HttpMethod method, string url, object? body, bool anonymous = false)
        {
            var (response, error) = await TrySendAsync(method, url, body, anonymous);
            if (response is null)
                return (default, error);

            using (response)
                return (await response.Content.ReadFromJsonAsync<T>(), null);
        }

        public async Task<string?> SendAsync(HttpMethod method, string url, object? body = null, bool anonymous = false)
        {
            var (response, error) = await TrySendAsync(method, url, body, anonymous);
            response?.Dispose();
            return error;
        }

        private async Task<(HttpResponseMessage? Response, string? Error)> TrySendAsync(
            HttpMethod method, string url, object? body, bool anonymous, string? notFoundMessage = null)
        {
            try
            {
                using var request = new HttpRequestMessage(method, url);
                if (body is not null)
                    request.Content = JsonContent.Create(body);

                var response = await factory.CreateClient(anonymous ? AnonymousClient : AuthorizedClient).SendAsync(request);
                if (response.IsSuccessStatusCode)
                    return (response, null);

                using (response)
                    return (null, response.StatusCode == System.Net.HttpStatusCode.NotFound && notFoundMessage is not null
                        ? notFoundMessage
                        : await DescribeFailureAsync(response));
            }
            catch (AccessTokenNotAvailableException ex)
            {
                ex.Redirect();
                return (null, "Your session has expired. Please sign in again.");
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "{Method} {Url} failed", method, url);
                return (null, "The service is unreachable. Please try again.");
            }
        }

        internal static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject()
                        .SelectMany(field => field.Value.ValueKind == JsonValueKind.Array
                            ? field.Value.EnumerateArray().Select(m => m.GetString())
                            : [field.Value.GetString()])
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Distinct()
                        .ToList();
                    if (messages.Count > 0)
                        return string.Join(" ", messages);
                }

                if (json.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
                    return text;
            }
            catch (JsonException)
            {
            }

            return $"Request failed ({(int)response.StatusCode})";
        }
    }

    public static class ApiQuery
    {
        public static string Of(params (string Name, object? Value)[] parameters) =>
            string.Join('&', parameters
                .Where(p => p.Value is not null && p.Value is not string { Length: 0 })
                .Select(p => $"{p.Name}={Uri.EscapeDataString(Format(p.Value!))}"));

        private static string Format(object value) => value switch
        {
            string s => s.Trim(),
            Enum e => Convert.ToInt32(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}