namespace Modules.Transactions.Application.Common.Options
{
    public sealed class IngestionOptions
    {
        public const string SectionName = "Ingestion";

        public const int DefaultMaxTransactionsPerDelivery = 500;

        public const int MaxAllowedTransactionsPerDelivery = 5_000;

        public int MaxTransactionsPerDelivery { get; set; } = DefaultMaxTransactionsPerDelivery;

        public static bool IsValid(IngestionOptions options) =>
            options.MaxTransactionsPerDelivery is >= 1 and <= MaxAllowedTransactionsPerDelivery;
    }
}