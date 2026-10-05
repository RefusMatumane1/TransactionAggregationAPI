using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Messaging.Persistence
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

            builder.Property(m => m.IdempotencyKey)
                .HasMaxLength(200);

            builder.Property(m => m.Channel)
                .HasMaxLength(32);

            builder.Property(m => m.CorrelationId)
                .HasMaxLength(InboxMessage.MaxCorrelationIdLength);

            builder.Property(m => m.TraceParent)
                .HasMaxLength(InboxMessage.MaxTraceParentLength);

            builder.Property(m => m.PayloadHash)
                .HasMaxLength(80);

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

            builder.HasIndex(m => m.ProcessedAt)
                .HasFilter("\"Status\" = 2")
                .HasDatabaseName("IX_InboxMessages_Processed_ProcessedAt");

            builder.HasIndex(m => new { m.SourceName, m.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("IX_InboxMessages_SourceName_IdempotencyKey");
        }
    }
}