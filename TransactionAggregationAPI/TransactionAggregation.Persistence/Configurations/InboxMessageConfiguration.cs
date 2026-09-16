using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Inbox;

namespace TransactionAggregation.Persistence.Configurations
{
    public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
    {
        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {
            builder.ToTable("InboxMessages");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id)
                .HasConversion(
                    id => id.Value,
                    value => InboxMessageId.CreateFrom(value))
                .ValueGeneratedNever();

            builder.Property(m => m.SourceName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(m => m.Payload)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(m => m.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(m => m.Attempts)
                .IsRequired();

            builder.Property(m => m.ReceivedAt).IsRequired();
            builder.Property(m => m.ClaimedAt);
            builder.Property(m => m.NextAttemptAt);
            builder.Property(m => m.ProcessedAt);

            builder.Property(m => m.LastError)
                .HasMaxLength(2000);

            builder.HasIndex(m => new { m.Status, m.NextAttemptAt })
                            .HasDatabaseName("IX_InboxMessages_Status_NextAttemptAt");
        }
    }
}