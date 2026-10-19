using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class ReportingAndCurrencyModuleIsolationTests
    {
        private static readonly Assembly ReportingApplication = typeof(Modules.Reporting.ReportingApplicationDependencyInjection).Assembly;
        private static readonly Assembly ReportingInfrastructure = typeof(Modules.Reporting.Infrastructure.Persistence.ReportingDbContext).Assembly;
        private static readonly Assembly CurrencyContracts = typeof(Modules.Currency.Contracts.ICurrencyCatalog).Assembly;
        private static readonly Assembly CurrencyApplication = typeof(Modules.Currency.CurrencyDependencyInjection).Assembly;

        // What a module may not touch of another: everything but its published *.Contracts.
        private static readonly string[] TransactionsInternals =
        [
            "Modules.Transactions.Domain", "Modules.Transactions.Application", "Modules.Transactions.Infrastructure", "Modules.Transactions.Presentation"
        ];

        private static readonly string[] OtherModulesInternals =
        [
            .. TransactionsInternals,
            "Modules.Customers.Domain", "Modules.Customers.Application", "Modules.Customers.Infrastructure",
            "Modules.WebhookSources.Domain", "Modules.WebhookSources.Application", "Modules.WebhookSources.Infrastructure",
            "Modules.Notifications.Domain", "Modules.Notifications.Application", "Modules.Notifications.Infrastructure",
            "Modules.Audit.Domain", "Modules.Audit.Application", "Modules.Audit.Infrastructure"
        ];

        [Fact]
        public void ReportingApplication_ReadsTheLedgerOnlyThroughItsPublishedLanguage() =>
            Types.InAssembly(ReportingApplication).Should()
                .NotHaveDependencyOnAny([.. OtherModulesInternals, "Modules.Reporting.Infrastructure", "Modules.Reporting.Presentation"])
                .GetResult().IsSuccessful.Should().BeTrue("Reporting knows the ledger by Transactions.Contracts (categories, statuses) and nothing deeper");

        [Fact]
        public void ReportingInfrastructure_DoesNotReachIntoAnotherModulesInternals() =>
            Types.InAssembly(ReportingInfrastructure).Should()
                .NotHaveDependencyOnAny([.. OtherModulesInternals, "Modules.Reporting.Presentation"])
                .GetResult().IsSuccessful.Should().BeTrue();

        [Fact]
        public void TransactionsDoesNotKnowReporting()
        {
            var transactions = new[]
            {
                typeof(Modules.Transactions.Domain.Entities.Transaction).Assembly,
                typeof(Modules.Transactions.Contracts.TransactionCategory).Assembly,
                typeof(Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext).Assembly
            };

            foreach (var assembly in transactions)
                Types.InAssembly(assembly).Should().NotHaveDependencyOnAny("Modules.Reporting")
                    .GetResult().IsSuccessful.Should().BeTrue(
                        $"{assembly.GetName().Name} publishes the ledger; Reporting is a consumer and the dependency runs one way");
        }

        [Fact]
        public void TheLedgerQueries_DoNotDependOnReporting() =>
            Types.InAssembly(typeof(Modules.Transactions.Application.Common.Interfaces.ITransactionsDbContext).Assembly).Should()
                .NotHaveDependencyOnAny("Modules.Reporting")
                .GetResult().IsSuccessful.Should().BeTrue();

        [Fact]
        public void CurrencyContracts_DependOnNothingOfOurs() =>
            Types.InAssembly(CurrencyContracts).Should()
                .NotHaveDependencyOnAny("Modules.Currency.Application", "Modules.Currency.Presentation", "Modules.Transactions", "Modules.Reporting", "BuildingBlocks", "SharedKernel")
                .GetResult().IsSuccessful.Should().BeTrue("the catalog interface is a leaf: every module may take it without taking anything else");

        [Fact]
        public void CurrencyApplication_KnowsNoOtherModule() =>
            Types.InAssembly(CurrencyApplication).Should()
                .NotHaveDependencyOnAny(["Modules.Transactions", "Modules.Reporting", "Modules.Customers", "Modules.WebhookSources",
                    "Modules.Notifications", "Modules.Audit"])
                .GetResult().IsSuccessful.Should().BeTrue("reference data serves the modules; it must not depend on any of them");

        [Fact]
        public void NoModule_ReachesIntoCurrencyInternals()
        {
            var consumers = new[]
            {
                typeof(Modules.Transactions.Domain.Entities.Transaction).Assembly,
                ReportingApplication,
                ReportingInfrastructure,
                typeof(Modules.Customers.Domain.Customer).Assembly,
                typeof(Modules.Audit.Domain.AuditEvent).Assembly
            };

            foreach (var assembly in consumers)
                Types.InAssembly(assembly).Should()
                    .NotHaveDependencyOnAny("Modules.Currency.Application", "Modules.Currency.Presentation")
                    .GetResult().IsSuccessful.Should().BeTrue($"{assembly.GetName().Name} may use currencies only through Modules.Currency.Contracts");
        }
    }
}
