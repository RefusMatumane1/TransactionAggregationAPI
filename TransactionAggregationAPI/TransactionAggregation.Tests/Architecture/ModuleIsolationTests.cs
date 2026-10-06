using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class ModuleIsolationTests
    {
        private static readonly Assembly WebhookSourcesDomainAssembly = typeof(Modules.WebhookSources.Domain.WebhookSource).Assembly;
        private static readonly Assembly WebhookSourcesApplicationAssembly = typeof(Modules.WebhookSources.AssemblyReference).Assembly;
        private static readonly Assembly WebhookSourcesInfrastructureAssembly = typeof(Modules.WebhookSources.Infrastructure.Persistence.WebhookSourcesDbContext).Assembly;
        private static readonly Assembly MessagingAssembly = typeof(BuildingBlocks.Messaging.Inbox.InboxMessage).Assembly;

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
                    "Modules.WebhookSources",
                    "Modules.Customers")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: "BuildingBlocks.Messaging is a shared reliability building block every module may depend on — if it depended back on a business module, it wouldn't be reusable and modules would be coupled through it transitively");
        }
    }
}