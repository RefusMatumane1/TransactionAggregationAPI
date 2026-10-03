using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Messaging.Persistence
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("OutboxMessages");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.Id)
                .HasConversion(
                    id => id.Value,
                    value => OutboxMessageId.CreateFrom(value))
                .ValueGeneratedNever();

            builder.Property(m => m.Type)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(m => m.Payload)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(m => m.SchemaVersion)
                .HasDefaultValue(1)
                .IsRequired();

            builder.Property(m => m.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(m => m.Attempts)
                .IsRequired();

            builder.Property(m => m.OccurredAt).IsRequired();
            builder.Property(m => m.ClaimedAt);
            builder.Property(m => m.NextAttemptAt);
            builder.Property(m => m.ProcessedAt);

            builder.Property(m => m.LastError)
                .HasMaxLength(OutboxMessage.MaxErrorLength);

            builder.Property(m => m.TraceParent).HasMaxLength(OutboxMessage.MaxTraceContextLength);
            builder.Property(m => m.CorrelationId).HasMaxLength(OutboxMessage.MaxTraceContextLength);

            builder.HasIndex(m => new { m.Status, m.NextAttemptAt })
                .HasDatabaseName("IX_OutboxMessages_Status_NextAttemptAt");

            builder.HasIndex(m => m.ProcessedAt)
                .HasFilter("\"Status\" = 2")
                .HasDatabaseName("IX_OutboxMessages_Processed_ProcessedAt");
        }
    }
}