using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace TransactionAggregation.Tests.Architecture;

/// <summary>
/// LayerDependencyTests enforces horizontal layering (Domain must not depend on
/// Infrastructure, etc.) within the original shared projects — it enforces nothing
/// about module isolation, which was the actual gap this restructuring closes (see
/// docs/adr/0001-modular-monolith-not-microservices.md and
/// docs/adr/0009-schema-per-module-database-strategy.md). These tests make the two
/// module boundaries introduced so far compiler/CI-enforced, not just documented:
/// WebhookSources must depend on nothing but SharedKernel, and BuildingBlocks.Messaging
/// — a shared building block every future module may depend on — must depend on no
/// business module at all, proving it's genuinely generic and not secretly
/// Transactions-shaped (it dispatches by message.Type strings, not by referencing
/// TransactionAggregation.Application directly).
/// </summary>
public class ModuleIsolationTests
{
    private static readonly Assembly WebhookSourcesAssembly = typeof(Modules.WebhookSources.WebhookSource).Assembly;
    private static readonly Assembly MessagingAssembly = typeof(BuildingBlocks.Messaging.Inbox.InboxMessage).Assembly;

    [Fact]
    public void WebhookSources_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(WebhookSourcesAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "TransactionAggregation.Domain",
                "TransactionAggregation.Application",
                "TransactionAggregation.Infrastructure",
                "TransactionAggregation.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "the WebhookSources module must depend only on SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void Messaging_ShouldNotDependOn_AnyBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(MessagingAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "TransactionAggregation.Domain",
                "TransactionAggregation.Application",
                "TransactionAggregation.Infrastructure",
                "TransactionAggregation.Persistence",
                "Modules.WebhookSources")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BuildingBlocks.Messaging is a shared reliability building block every module may depend on — if it depended back on a business module, it wouldn't be reusable and modules would be coupled through it transitively");
    }
}
