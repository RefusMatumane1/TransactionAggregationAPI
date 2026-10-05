using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Audit.Domain;
using System.Text.Json;

namespace Modules.Audit.Infrastructure.Persistence
{
    public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
    {
        public void Configure(EntityTypeBuilder<AuditEvent> builder)
        {
            builder.ToTable("AuditEvents");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            builder.Property(e => e.OccurredAt).IsRequired();
            builder.Property(e => e.RecordedAt).IsRequired();
            builder.Property(e => e.Channel).HasMaxLength(32).IsRequired();
            builder.Property(e => e.SourceName).HasMaxLength(AuditEvent.MaxIdentifierLength).IsRequired();
            builder.Property(e => e.ExternalAccountId).HasMaxLength(AuditEvent.MaxIdentifierLength);
            builder.Property(e => e.IdempotencyKey).HasMaxLength(AuditEvent.MaxIdentifierLength);
            builder.Property(e => e.ExternalTransactionId).HasMaxLength(AuditEvent.MaxExternalTransactionIdLength);
            builder.Property(e => e.Detail).HasMaxLength(AuditEvent.MaxDetailLength);
            builder.Property(e => e.TraceId).HasMaxLength(AuditEvent.MaxTraceIdLength);
            builder.Property(e => e.Actor).HasMaxLength(AuditEvent.MaxActorLength);

            builder.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new(),
                    new ValueComparer<Dictionary<string, string>>(
                        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                        c => c == null ? 0 : c.Aggregate(0, (h, kv) => HashCode.Combine(h, kv.GetHashCode())),
                        c => c == null ? new() : new Dictionary<string, string>(c)))
                .HasColumnType("jsonb")
                .IsRequired();

            builder.HasIndex(e => e.OccurredAt).HasDatabaseName("IX_AuditEvents_OccurredAt");
            builder.HasIndex(e => e.InboxMessageId).HasDatabaseName("IX_AuditEvents_InboxMessageId");
            builder.HasIndex(e => e.TransactionId).HasDatabaseName("IX_AuditEvents_TransactionId");
            builder.HasIndex(e => new { e.Channel, e.SourceName, e.OccurredAt }).HasDatabaseName("IX_AuditEvents_Channel_Source_OccurredAt");
            builder.HasIndex(e => new { e.ExternalAccountId, e.OccurredAt }).HasDatabaseName("IX_AuditEvents_ExternalAccountId_OccurredAt");
            builder.HasIndex(e => new { e.EventType, e.OccurredAt }).HasDatabaseName("IX_AuditEvents_EventType_OccurredAt");
            builder.HasIndex(e => new { e.Actor, e.OccurredAt })
                .HasFilter("\"Actor\" IS NOT NULL")
                .HasDatabaseName("IX_AuditEvents_Actor_OccurredAt");
        }
    }
}