namespace Modules.Transactions.Application.Common.Options
{
    public sealed class IngestionOptions
    {
        public const string SectionName = "Ingestion";

        public const int DefaultMaxTransactionsPerDelivery = 500;

        // Upper bound on the setting: past this, one delivery would hold a database transaction open for too long.
        public const int MaxAllowedTransactionsPerDelivery = 5_000;

        // Largest number of transactions one webhook call or Kafka record may carry. Both channels share it.
        public int MaxTransactionsPerDelivery { get; set; } = DefaultMaxTransactionsPerDelivery;

        public static bool IsValid(IngestionOptions options) =>
            options.MaxTransactionsPerDelivery is >= 1 and <= MaxAllowedTransactionsPerDelivery;
    }
}