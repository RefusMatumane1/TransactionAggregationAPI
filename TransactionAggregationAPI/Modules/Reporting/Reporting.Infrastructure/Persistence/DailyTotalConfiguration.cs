using BuildingBlocks.Application.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Reporting.Application.Aggregation;
using SharedKernel.Common.ValueObjects;

namespace Modules.Reporting.Infrastructure.Persistence
{
    public class DailyTotalConfiguration : IEntityTypeConfiguration<DailyTotal>
    {
        public const string TableName = "DailyTotals";

        private const int TotalPrecision = 28;

        public void Configure(EntityTypeBuilder<DailyTotal> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(d => new { d.SourceName, d.ExternalAccountId, d.Day, d.Category, d.Currency });

            builder.Property(d => d.SourceName).HasMaxLength(AccountFilter.MaxInstitutionLength);
            builder.Property(d => d.ExternalAccountId).HasMaxLength(AccountFilter.MaxExternalAccountIdLength);
            builder.Property(d => d.Category).HasConversion<int>();
            builder.Property(d => d.Currency).HasMaxLength(3);
            builder.Property(d => d.Income).HasPrecision(TotalPrecision, MoneyScale.Max);
            builder.Property(d => d.Expenses).HasPrecision(TotalPrecision, MoneyScale.Max);
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