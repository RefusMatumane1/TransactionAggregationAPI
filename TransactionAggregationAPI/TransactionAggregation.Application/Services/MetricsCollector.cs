

//namespace TransactionAggregation.Application.Services
//{
//    /// <summary>
//    /// Production metrics collector using Prometheus
//    /// </summary>
//    public class MetricsCollector : IMetricsCollector
//    {
//        private readonly Counter _totalTransactionsCounter;
//        private readonly Histogram _transactionAmountHistogram;
//        private readonly Counter _errorsCounter;
//        private readonly Gauge _activeTransactionsGauge;

//        public MetricsCollector()
//        {
//            _totalTransactionsCounter = Metrics.CreateCounter(
//                "transactions_total",
//                "Total number of transactions processed",
//                new CounterConfiguration
//                {
//                    LabelNames = new[] { "type", "status", "source" }
//                });

//            _transactionAmountHistogram = Metrics.CreateHistogram(
//                "transaction_amount",
//                "Distribution of transaction amounts",
//                new HistogramConfiguration
//                {
//                    LabelNames = new[] { "currency", "category" },
//                    Buckets = new[] { 10, 50, 100, 500, 1000, 5000, 10000 }
//                });

//            _errorsCounter = Metrics.CreateCounter(
//                "errors_total",
//                "Total number of errors",
//                new CounterConfiguration
//                {
//                    LabelNames = new[] { "type", "source" }
//                });

//            _activeTransactionsGauge = Metrics.CreateGauge(
//                "active_transactions",
//                "Number of transactions currently being processed");
//        }

//        public void RecordMetric(string metricName, double value, Dictionary<string, string>? tags = null)
//        {
//            switch (metricName)
//            {
//                case "transactions.created":
//                    _totalTransactionsCounter.WithLabels(
//                        tags?.GetValueOrDefault("is_income") == "True" ? "income" : "expense",
//                        "created",
//                        tags?.GetValueOrDefault("source") ?? "unknown"
//                    ).Inc();
//                    break;

//                case "transactions.approved":
//                    _totalTransactionsCounter.WithLabels(
//                        "all",
//                        "approved",
//                        tags?.GetValueOrDefault("source") ?? "unknown"
//                    ).Inc();
//                    break;

//                case "transactions.rejected":
//                    _totalTransactionsCounter.WithLabels(
//                        "all",
//                        "rejected",
//                        tags?.GetValueOrDefault("source") ?? "unknown"
//                    ).Inc();
//                    break;

//                case "errors.total":
//                    _errorsCounter.WithLabels(
//                        tags?.GetValueOrDefault("exception_type") ?? "unknown",
//                        tags?.GetValueOrDefault("context") ?? "unknown"
//                    ).Inc();
//                    break;
//            }

//            if (metricName == "transaction.amount")
//            {
//                _transactionAmountHistogram.WithLabels(
//                    tags?.GetValueOrDefault("currency") ?? "USD",
//                    tags?.GetValueOrDefault("category") ?? "other"
//                ).Observe(value);
//            }
//        }

//        public void IncrementActiveTransactions() => _activeTransactionsGauge.Inc();
//        public void DecrementActiveTransactions() => _activeTransactionsGauge.Dec();
//    }

//    public interface IMetricsCollector
//    {
//        void RecordMetric(string metricName, double value, Dictionary<string, string>? tags = null);
//        void IncrementActiveTransactions();
//        void DecrementActiveTransactions();
//    }
//}
