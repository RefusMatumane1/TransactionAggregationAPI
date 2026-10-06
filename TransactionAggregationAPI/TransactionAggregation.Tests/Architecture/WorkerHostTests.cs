using FluentAssertions;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using TransactionAggregation.Worker.BackgroundServices;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public class WorkerHostTests
    {
        private static readonly Assembly WorkerAssembly = typeof(PollingBackgroundService).Assembly;

        private static IEnumerable<Assembly> ApiAssemblies()
        {
            var seen = new Dictionary<string, Assembly>();
            var pending = new Queue<Assembly>([typeof(Program).Assembly]);
            while (pending.TryDequeue(out var assembly))
            {
                if (!seen.TryAdd(assembly.GetName().Name!, assembly))
                    continue;
                foreach (var reference in assembly.GetReferencedAssemblies().Where(IsOurs))
                    pending.Enqueue(Assembly.Load(reference));
            }
            return seen.Values;
        }

        private static bool IsOurs(AssemblyName name) =>
            name.Name!.StartsWith("Modules.", StringComparison.Ordinal)
            || name.Name.StartsWith("BuildingBlocks", StringComparison.Ordinal)
            || name.Name.StartsWith("SharedKernel", StringComparison.Ordinal)
            || name.Name.StartsWith("Transactions.", StringComparison.Ordinal)
            || name.Name.StartsWith("WebhookSources.", StringComparison.Ordinal)
            || name.Name.StartsWith("Customers.", StringComparison.Ordinal)
            || name.Name.StartsWith("Audit.", StringComparison.Ordinal)
            || name.Name.StartsWith("TransactionAggregation", StringComparison.Ordinal);

        [Fact]
        public void TheApiAssemblyWalk_ReachesTheModules()
        {
            ApiAssemblies().Select(a => a.GetName().Name).Should().Contain(
                ["Transactions.Infrastructure", "BuildingBlocks.Messaging", "BuildingBlocks.Persistence", "TransactionAggregation.Hosting"],
                "otherwise the checks below would pass by looking at nothing");
        }

        [Fact]
        public void TheApi_ContainsNoBackgroundService()
        {
            var hosted = ApiAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IHostedService).IsAssignableFrom(t) && !t.IsInterface)
                .Select(t => t.FullName)
                .ToList();

            hosted.Should().BeEmpty("background processing belongs in TransactionAggregation.Worker, not in code the API loads");
        }

        [Fact]
        public void TheApi_DoesNotLoadTheWorkerOrKafka()
        {
            var names = ApiAssemblies()
                .SelectMany(a => a.GetReferencedAssemblies().Append(a.GetName()))
                .Select(n => n.Name)
                .ToList();

            names.Should().NotContain(WorkerAssembly.GetName().Name);
            names.Should().NotContain("Confluent.Kafka", "only the worker consumes Kafka");
        }

        [Fact]
        public void TheWorker_HostsEveryBackgroundService()
        {
            WorkerAssembly.GetTypes()
                .Where(t => typeof(BackgroundService).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.Name)
                .Should().BeEquivalentTo(
                    "InboxDispatcherBackgroundService",
                    "OutboxDispatcherBackgroundService",
                    "MessageArchiveBackgroundService",
                    "DailyTotalsRefreshBackgroundService",
                    "BankTransactionsKafkaConsumer");
        }
    }
}