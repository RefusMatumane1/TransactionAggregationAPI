using FluentAssertions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Linq;
using TransactionAggregation.Application.Commands.Customer.CreateCustomer;
using TransactionAggregationAPI.Logging;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Logging;

public class SensitiveDataDestructuringPolicyTests
{
    private sealed class CapturingSink : ILogEventSink
    {
        public LogEvent? LastEvent { get; private set; }
        public void Emit(LogEvent logEvent) => LastEvent = logEvent;
    }

    [Fact]
    public void Destructure_RedactsPropertyMarkedSensitive()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var command = new CreateCustomerCommand("user@example.com", "Test User", "SuperSecret123");

        logger.Information("Request {@Request}", command);

        sink.LastEvent.Should().NotBeNull();
        var structure = (StructureValue)sink.LastEvent!.Properties["Request"];
        var passwordProperty = structure.Properties.Single(p => p.Name == "Password");
        var passwordValue = ((ScalarValue)passwordProperty.Value).Value as string;

        passwordValue.Should().Be("***REDACTED***");
        sink.LastEvent.RenderMessage().Should().NotContain("SuperSecret123");
    }

    [Fact]
    public void Destructure_LeavesTypesWithNoSensitivePropertiesUntouched()
    {
        var sink = new CapturingSink();
        var logger = new LoggerConfiguration()
            .Destructure.With<SensitiveDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Plain {@Value}", new { Name = "hello" });

        sink.LastEvent.Should().NotBeNull();
        sink.LastEvent!.RenderMessage().Should().Contain("hello");
    }
}