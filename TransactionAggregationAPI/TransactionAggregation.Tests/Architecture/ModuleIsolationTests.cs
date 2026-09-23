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
/// WebhookSources (split into Domain/Application/Infrastructure/Contracts projects —
/// each checked individually, plus intra-module layering between them) must depend
/// on nothing but SharedKernel, and BuildingBlocks.Messaging — a shared building
/// block every future module may depend on — must depend on no business module at
/// all, proving it's genuinely generic and not secretly Transactions-shaped (it
/// dispatches by message.Type strings, not by referencing
/// Modules.Transactions.Application directly).
/// </summary>
public class ModuleIsolationTests
{
    private static readonly Assembly WebhookSourcesDomainAssembly = typeof(Modules.WebhookSources.Domain.WebhookSource).Assembly;
    private static readonly Assembly WebhookSourcesApplicationAssembly = typeof(Modules.WebhookSources.AssemblyReference).Assembly;
    private static readonly Assembly WebhookSourcesInfrastructureAssembly = typeof(Modules.WebhookSources.Infrastructure.Persistence.WebhookSourcesDbContext).Assembly;
    private static readonly Assembly MessagingAssembly = typeof(BuildingBlocks.Messaging.Inbox.InboxMessage).Assembly;
    private static readonly Assembly BankLinksDomainAssembly = typeof(Modules.BankLinks.Domain.BankLink).Assembly;
    private static readonly Assembly BankLinksApplicationAssembly = typeof(Modules.BankLinks.AssemblyReference).Assembly;
    private static readonly Assembly BankLinksInfrastructureAssembly = typeof(Modules.BankLinks.Infrastructure.Persistence.BankLinksDbContext).Assembly;
    private static readonly Assembly BankLinksContractsAssembly = typeof(Modules.BankLinks.Contracts.IBankLinksReadApi).Assembly;

    private static readonly string[] LegacyLayers =
    [
        "Modules.Transactions.Domain",
        "Modules.Transactions.Application",
        "Modules.Transactions.Infrastructure",
        "Modules.Transactions.Infrastructure.Persistence"
    ];

    [Fact]
    public void WebhookSourcesDomain_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(WebhookSourcesDomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(LegacyLayers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "WebhookSources.Domain must depend only on SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void WebhookSourcesApplication_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(WebhookSourcesApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(LegacyLayers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "WebhookSources.Application must depend only on its own Domain/Contracts and SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void WebhookSourcesInfrastructure_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(WebhookSourcesInfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny(LegacyLayers)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "WebhookSources.Infrastructure must depend only on its own Application/Domain/Contracts and SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void WebhookSourcesDomain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(WebhookSourcesDomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Modules.WebhookSources.Application", "Modules.WebhookSources.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain is the innermost layer of the module — it must not depend outward on Application or Infrastructure");
    }

    [Fact]
    public void WebhookSourcesApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(WebhookSourcesApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny("Modules.WebhookSources.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must depend only on Domain/Contracts (+ SharedKernel) — never on Infrastructure, which depends inward on Application, not the reverse");
    }

    [Fact]
    public void Messaging_ShouldNotDependOn_AnyBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(MessagingAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Modules.Transactions.Domain",
                "Modules.Transactions.Application",
                "Modules.Transactions.Infrastructure",
                "Modules.Transactions.Infrastructure.Persistence",
                "Modules.WebhookSources")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BuildingBlocks.Messaging is a shared reliability building block every module may depend on — if it depended back on a business module, it wouldn't be reusable and modules would be coupled through it transitively");
    }

    [Fact]
    public void BankLinksDomain_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(BankLinksDomainAssembly)
            .Should()
            .NotHaveDependencyOnAny([.. LegacyLayers, "Modules.WebhookSources"])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BankLinks.Domain must depend only on SharedKernel — the module needs an Account to exist but reaches it through IAccountProvisioningPort (a port it owns), never by referencing the Account entity or ITransactionsDbContext directly");
    }

    [Fact]
    public void BankLinksApplication_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(BankLinksApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny([.. LegacyLayers, "Modules.WebhookSources"])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BankLinks.Application must depend only on its own Domain/Contracts and SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void BankLinksInfrastructure_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(BankLinksInfrastructureAssembly)
            .Should()
            .NotHaveDependencyOnAny([.. LegacyLayers, "Modules.WebhookSources"])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BankLinks.Infrastructure must depend only on its own Application/Domain/Contracts and SharedKernel — reaching into the legacy layers or another module defeats the point of extracting it");
    }

    [Fact]
    public void BankLinksContracts_ShouldNotDependOn_AnyOtherBusinessModuleOrLegacyLayer()
    {
        var result = Types.InAssembly(BankLinksContractsAssembly)
            .Should()
            .NotHaveDependencyOnAny([.. LegacyLayers, "Modules.WebhookSources"])
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "BankLinks.Contracts must depend only on SharedKernel — it's a leaf project (interfaces + DTOs only) so other modules can reference it without pulling in EF Core or any implementation");
    }

    [Fact]
    public void BankLinksDomain_ShouldNotDependOn_ApplicationOrInfrastructure()
    {
        var result = Types.InAssembly(BankLinksDomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Modules.BankLinks.Application", "Modules.BankLinks.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain is the innermost layer of the module — it must not depend outward on Application or Infrastructure");
    }

    [Fact]
    public void BankLinksApplication_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(BankLinksApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny("Modules.BankLinks.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application must depend only on Domain/Contracts (+ SharedKernel) — never on Infrastructure, which depends inward on Application, not the reverse");
    }
}
