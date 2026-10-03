using BuildingBlocks.Messaging.Archiving;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocks.Messaging.Persistence
{
    public sealed class ArchivedInboxMessageConfiguration : IEntityTypeConfiguration<ArchivedInboxMessage>
    {
        public void Configure(EntityTypeBuilder<ArchivedInboxMessage> builder)
        {
            builder.ToTable("InboxMessagesArchive");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.SourceName).HasMaxLength(200).IsRequired();
            builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.IdempotencyKey).HasMaxLength(200);
            builder.Property(m => m.Channel).HasMaxLength(32);
            builder.Property(m => m.CorrelationId).HasMaxLength(Inbox.InboxMessage.MaxCorrelationIdLength);
            builder.Property(m => m.TraceParent).HasMaxLength(Inbox.InboxMessage.MaxTraceParentLength);
            builder.Property(m => m.PayloadHash).HasMaxLength(80);
            builder.Property(m => m.Status).HasConversion<int>().IsRequired();
            builder.Property(m => m.LastError).HasMaxLength(2000);
            builder.Property(m => m.ArchivedAt).IsRequired();

            builder.HasIndex(m => new { m.SourceName, m.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL")
                .HasDatabaseName("IX_InboxMessagesArchive_SourceName_IdempotencyKey");

            builder.HasIndex(m => m.ReceivedAt).HasDatabaseName("IX_InboxMessagesArchive_ReceivedAt");
        }
    }

    public sealed class ArchivedOutboxMessageConfiguration : IEntityTypeConfiguration<ArchivedOutboxMessage>
    {
        public void Configure(EntityTypeBuilder<ArchivedOutboxMessage> builder)
        {
            builder.ToTable("OutboxMessagesArchive");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();
            builder.Property(m => m.Type).HasMaxLength(100).IsRequired();
            builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.TraceParent).HasMaxLength(Outbox.OutboxMessage.MaxTraceContextLength);
            builder.Property(m => m.CorrelationId).HasMaxLength(Outbox.OutboxMessage.MaxTraceContextLength);
            builder.Property(m => m.Status).HasConversion<int>().IsRequired();
            builder.Property(m => m.LastError).HasMaxLength(2000);
            builder.Property(m => m.ArchivedAt).IsRequired();

            builder.HasIndex(m => m.OccurredAt).HasDatabaseName("IX_OutboxMessagesArchive_OccurredAt");
        }
    }
}