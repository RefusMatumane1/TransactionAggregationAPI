using BuildingBlocks.Messaging.Publishing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionAggregation.Worker;
using TransactionAggregation.Worker.Messaging;
using TransactionAggregation.Worker.Notifications;
using TransactionAggregation.Worker.Reporting;
using TransactionAggregation.Worker.Transactions;
using Xunit;

namespace TransactionAggregation.Tests.Unit.BackgroundServices
{
    /// <summary>What the Worker hosts, module by module, and how configuration switches the optional parts off.</summary>
    public class BackgroundProcessingRegistrationTests
    {
        private static ServiceCollection Register(Dictionary<string, string?>? settings = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"ConnectionStrings:{KafkaOptions.ConnectionStringName}"] = "localhost:9092" }
                    .Concat(settings ?? []).ToDictionary(p => p.Key, p => p.Value))
                .Build();

            var services = new ServiceCollection();
            services.AddBackgroundProcessing(configuration);
            return services;
        }

        private static List<Type> Hosted(IServiceCollection services) =>
            services.Where(d => d.ServiceType == typeof(IHostedService)).Select(d => d.ImplementationType!).ToList();

        private static List<Type> OutboxHandlers(IServiceCollection services) =>
            services.Where(d => d.ServiceType == typeof(IOutboxMessageHandler)).Select(d => d.ImplementationType!).ToList();

        [Fact]
        public void EveryModule_ContributesItsOwnProcessing() =>
            Hosted(Register()).Should().BeEquivalentTo(
            [
                typeof(OutboxDispatcherBackgroundService), typeof(MessageArchiveBackgroundService),
                typeof(InboxDispatcherBackgroundService), typeof(BankTransactionsKafkaConsumer),
                typeof(DailyTotalsRefreshBackgroundService)
            ]);

        [Fact]
        public void TheOutbox_KnowsHowToHandleEachEventTheModulesPublish() =>
            OutboxHandlers(Register()).Should().BeEquivalentTo([typeof(TransactionRecordedHandler), typeof(DuplicateInboundDetectedHandler)]);

        [Theory]
        [InlineData("Aggregation:Enabled", typeof(DailyTotalsRefreshBackgroundService))]
        [InlineData("MessageArchive:Enabled", typeof(MessageArchiveBackgroundService))]
        [InlineData("Kafka:Enabled", typeof(BankTransactionsKafkaConsumer))]
        public void AnOptionalService_CanBeSwitchedOff_WithoutAffectingTheOthers(string setting, Type service)
        {
            var hosted = Hosted(Register(new() { [setting] = "false" }));

            hosted.Should().NotContain(service).And.HaveCount(4);
        }

        [Fact]
        public void TheDispatchersThemselves_CannotBeSwitchedOff()
        {
            var hosted = Hosted(Register(new() { ["Outbox:Enabled"] = "false", ["Inbox:Enabled"] = "false" }));

            hosted.Should().Contain([typeof(OutboxDispatcherBackgroundService), typeof(InboxDispatcherBackgroundService)]);
        }

        [Fact]
        public void WithoutAKafkaConnection_TheWorkerRefusesToStart_BecauseTheOutboxCouldNotPublish()
        {
            var act = () => new ServiceCollection().AddBackgroundProcessing(new ConfigurationBuilder().Build());

            act.Should().Throw<InvalidOperationException>().WithMessage($"*ConnectionStrings:{KafkaOptions.ConnectionStringName}*");
        }

        [Fact]
        public void TheIntegrationEventPublisher_IsKafka() =>
            Register().Should().Contain(d => d.ServiceType == typeof(IIntegrationEventPublisher)
                                           && d.ImplementationType == typeof(KafkaIntegrationEventPublisher));
    }
}
