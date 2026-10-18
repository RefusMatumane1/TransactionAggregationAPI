using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class NotificationsModuleIsolationTests
    {
        private static readonly Assembly Domain = typeof(Modules.Notifications.Domain.AlertWebhook).Assembly;
        private static readonly Assembly Application = typeof(Modules.Notifications.NotificationsApplicationDependencyInjection).Assembly;
        private static readonly Assembly Infrastructure = typeof(Modules.Notifications.Infrastructure.Persistence.NotificationsDbContext).Assembly;
        private static readonly Assembly Contracts = typeof(Modules.Notifications.Contracts.IDuplicateAlertNotifier).Assembly;

        private static readonly string[] OtherModules =
        [
            "Modules.Transactions", "Modules.Customers", "Modules.Audit.Domain", "Modules.Audit.Application", "Modules.Audit.Infrastructure",
            "Modules.WebhookSources.Domain", "Modules.WebhookSources.Application", "Modules.WebhookSources.Infrastructure"
        ];

        [Fact]
        public void NotificationsContracts_DependOnNothingOfOurs() =>
            Types.InAssembly(Contracts).Should()
                .NotHaveDependencyOnAny("Modules.Notifications.Domain", "Modules.Notifications.Application", "Modules.Notifications.Infrastructure", "BuildingBlocks", "SharedKernel")
                .GetResult().IsSuccessful.Should().BeTrue("a published contract must not drag a module's internals or the shared kernel into its consumers");

        [Fact]
        public void NotificationsDomain_ShouldNotDependOn_ApplicationInfrastructureOrAnyOtherModule() =>
            Types.InAssembly(Domain).Should()
                .NotHaveDependencyOnAny(["Modules.Notifications.Application", "Modules.Notifications.Infrastructure", .. OtherModules])
                .GetResult().IsSuccessful.Should().BeTrue();

        [Fact]
        public void NotificationsApplication_ShouldNotDependOn_InfrastructureOrAnyOtherModulesInternals() =>
            Types.InAssembly(Application).Should()
                .NotHaveDependencyOnAny(["Modules.Notifications.Infrastructure", .. OtherModules])
                .GetResult().IsSuccessful.Should().BeTrue("a module reaches another only through its *.Contracts");

        [Fact]
        public void NotificationsInfrastructure_ShouldNotDependOn_AnyOtherModulesInternals() =>
            Types.InAssembly(Infrastructure).Should()
                .NotHaveDependencyOnAny(OtherModules)
                .GetResult().IsSuccessful.Should().BeTrue();

        [Fact]
        public void NoOtherModule_ReachesIntoNotificationsInternals()
        {
            var consumers = new[]
            {
                typeof(Modules.Transactions.Domain.Entities.Transaction).Assembly,
                typeof(Modules.Customers.Domain.Customer).Assembly,
                typeof(Modules.Audit.Domain.AuditEvent).Assembly,
                typeof(Modules.WebhookSources.Domain.WebhookSource).Assembly
            };

            foreach (var assembly in consumers)
                Types.InAssembly(assembly).Should()
                    .NotHaveDependencyOnAny("Modules.Notifications.Domain", "Modules.Notifications.Application", "Modules.Notifications.Infrastructure")
                    .GetResult().IsSuccessful.Should().BeTrue($"{assembly.GetName().Name} may use Notifications only through Modules.Notifications.Contracts");
        }
    }
}