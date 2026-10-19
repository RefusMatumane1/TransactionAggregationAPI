using BuildingBlocks.Application.Seeding;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence.Migrations;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit;
using Modules.Currency;
using Modules.Customers;
using Modules.Notifications;
using Modules.Reporting;
using Modules.Transactions;
using Modules.WebhookSources;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Persistence
{
    /// <summary>What the modules contribute to the host: the migrations it runs and the demo data it may load.</summary>
    public class ModuleRegistrationTests
    {
        // The order the host registers them in (HostingExtensions.AddApplicationModules, then the API's own Customers).
        private static ServiceCollection Modules(bool development)
        {
            var configuration = new ConfigurationBuilder().Build();
            var services = new ServiceCollection();

            services.AddCurrencyModule(configuration);
            services.AddTransactionsModule(configuration, development);
            services.AddReportingModule();
            services.AddMessagingBuildingBlock();
            services.AddWebhookSourcesModule(development);
            services.AddAuditModule();
            services.AddNotificationsModule(configuration);
            services.AddCustomersModule(development);
            return services;
        }

        private static List<string> Migrations(IServiceCollection services) =>
            services.Where(d => d.ServiceType == typeof(IModuleMigration))
                .Select(d => d.ImplementationType!.GenericTypeArguments.Single().Name)
                .ToList();

        private static List<string> Seeders(IServiceCollection services) =>
            services.Where(d => d.ServiceType == typeof(DevelopmentSeederRegistration))
                .Select(d => ((DevelopmentSeederRegistration)d.ImplementationInstance!).SeederType.Name)
                .ToList();

        [Fact]
        public void EveryModuleDatabase_IsMigrated_ExactlyOnce() =>
            Migrations(Modules(development: false)).Should().BeEquivalentTo(
                ["TransactionsDbContext", "ReportingDbContext", "MessagingDbContext", "WebhookSourcesDbContext", "AuditDbContext",
                 "NotificationsDbContext", "CustomersDbContext"],
                options => options.WithoutStrictOrdering())
                .And.OnlyHaveUniqueItems();

        [Fact]
        public void Transactions_AreMigratedBeforeReporting_BecauseReportingTakesOverTablesTheyCreate()
        {
            var order = Migrations(Modules(development: false));

            order.IndexOf("TransactionsDbContext").Should().BeLessThan(order.IndexOf("ReportingDbContext"));
        }

        [Fact]
        public void EnrollingAContextTwice_StillMigratesItOnce()
        {
            var services = Modules(development: false);
            services.AddReportingModule();
            services.AddTransactionsModule(new ConfigurationBuilder().Build());

            Migrations(services).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void InDevelopment_EachModuleBringsItsOwnDemoData()
        {
            Seeders(Modules(development: true)).Should().Equal(
                "TransactionsDemoSeeder", "MockAggregatorBankSeeder", "CustomersDemoSeeder");
        }

        [Fact]
        public void OutsideDevelopment_NoDemoDataIsRegistered_NorTheAccountsItWouldCreate()
        {
            var services = Modules(development: false);

            Seeders(services).Should().BeEmpty();
            services.Should().NotContain(d => d.ServiceType == typeof(Modules.Transactions.Contracts.IDemoAccountDirectory));
        }
    }
}
