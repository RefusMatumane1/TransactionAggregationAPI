using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;

namespace Modules.Transactions.Infrastructure.Persistence.Configurations
{
    public class DailyTotalConfiguration : IEntityTypeConfiguration<DailyTotal>
    {
        public const string TableName = "DailyTotals";

        // Sums of up to a day's entries for one account: wider than one ledger amount.
        private const int TotalPrecision = 28;

        public void Configure(EntityTypeBuilder<DailyTotal> builder)
        {
            builder.ToTable(TableName);

            // Account first, so a bank- or account-filtered read is a key range scan; the day index
            // for unfiltered reads is built CONCURRENTLY in the migration.
            builder.HasKey(d => new { d.SourceName, d.ExternalAccountId, d.Day, d.Category, d.Currency });

            builder.Property(d => d.SourceName).HasMaxLength(TransactionSource.MaxNameLength);
            builder.Property(d => d.ExternalAccountId).HasMaxLength(Transaction.MaxExternalAccountIdLength);
            builder.Property(d => d.Category).HasConversion<int>();
            builder.Property(d => d.Currency).HasMaxLength(3);
            builder.Property(d => d.Income).HasPrecision(TotalPrecision, Money.MaxScale);
            builder.Property(d => d.Expenses).HasPrecision(TotalPrecision, Money.MaxScale);
        }
    }

    public class AggregationCheckpointConfiguration : IEntityTypeConfiguration<AggregationCheckpoint>
    {
        public const string TableName = "AggregationCheckpoints";

        public void Configure(EntityTypeBuilder<AggregationCheckpoint> builder)
        {
            builder.ToTable(TableName);
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
        }
    }
}