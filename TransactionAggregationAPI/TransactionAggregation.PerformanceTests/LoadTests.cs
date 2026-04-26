//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace TransactionAggregation.PerformanceTests
//{
//    [SimpleJob(RuntimeMoniker.Net80)]
//    [MemoryDiagnoser]
//    [ThreadingDiagnoser]
//    public class TransactionAggregationBenchmarks
//    {
//        private IServiceProvider _services;
//        private ITransactionAggregator _aggregator;
//        private List<CustomerId> _customers;

//        [GlobalSetup]
//        public void Setup()
//        {
//            var services = new ServiceCollection();

//            // Register services
//            services.AddApplication();
//            services.AddInfrastructure(new ConfigurationBuilder().Build());
//            services.AddPersistence();

//            _services = services.BuildServiceProvider();
//            _aggregator = _services.GetRequiredService<ITransactionAggregator>();

//            // Generate test data
//            var faker = new Faker();
//            _customers = Enumerable.Range(1, 100)
//                .Select(_ => CustomerId.Create())
//                .ToList();
//        }

//        [Benchmark]
//        [Arguments(1)]
//        [Arguments(10)]
//        [Arguments(100)]
//        public async Task AggregateTransactions(int customerCount)
//        {
//            var tasks = _customers.Take(customerCount)
//                .Select(c => _aggregator.AggregateCustomerTransactionsAsync(c));

//            await Task.WhenAll(tasks);
//        }

//        [Benchmark]
//        public async Task ParallelAggregation()
//        {
//            var options = new ParallelOptions
//            {
//                MaxDegreeOfParallelism = Environment.ProcessorCount
//            };

//            await Parallel.ForEachAsync(_customers, options, async (customer, ct) =>
//            {
//                await _aggregator.AggregateCustomerTransactionsAsync(customer);
//            });
//        }
//    }
//}
