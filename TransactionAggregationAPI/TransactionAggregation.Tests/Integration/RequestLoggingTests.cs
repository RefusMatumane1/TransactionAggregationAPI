using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class RequestLoggingTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        private sealed class CapturingSink : ILogEventSink
        {
            public ConcurrentQueue<LogEvent> Events { get; } = new();
            public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
        }

        private async Task<LogEvent> RequestCompletionFor(string path)
        {
            var sink = new CapturingSink();
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            using var client = factory
                .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<Serilog.ILogger>(logger)))
                .CreateClient();

            await client.GetAsync(path);

            return sink.Events.Should().ContainSingle(e => e.Properties.ContainsKey("RoutePattern")).Subject;
        }

        private static string Everything(LogEvent logEvent) =>
            logEvent.RenderMessage() + string.Join("|", logEvent.Properties.Select(p => $"{p.Key}={p.Value}"));

        [Fact]
        public async Task MatchedRoute_LogsTheRoutePattern_NotTheValuesInThePath()
        {
            var transactionId = Guid.NewGuid().ToString();

            var logEvent = await RequestCompletionFor($"/api/v1/transactions/{transactionId}");

            logEvent.RenderMessage().Should().Contain("{id:guid}");
            Everything(logEvent).Should().NotContain(transactionId);
            logEvent.Properties.Should().NotContainKey("RequestPath");
        }

        [Fact]
        public async Task UnmatchedPath_CarryingAnEmail_IsNotLogged()
        {
            const string email = "jane.doe@example.com";

            var logEvent = await RequestCompletionFor($"/api/v1/lookup/email/{Uri.EscapeDataString(email)}");

            // Unknown API paths reach the 404 fallback, which logs its pattern, never the path.
            Everything(logEvent).Should().NotContain("jane.doe").And.Contain("api/{**path}");
        }
    }
}