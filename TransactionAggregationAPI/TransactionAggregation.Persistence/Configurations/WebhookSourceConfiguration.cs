using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Persistence.Configurations
{
    public class WebhookSourceConfiguration : IEntityTypeConfiguration<WebhookSource>
    {
        public void Configure(EntityTypeBuilder<WebhookSource> builder)
        {
            builder.ToTable("WebhookSources");

            builder.HasKey(s => s.Id);

            builder.Property(s => s.Id)
                .HasConversion(
                    id => id.Value,
                    value => WebhookSourceId.CreateFrom(value))
                .ValueGeneratedNever();

            builder.Property(s => s.Name)
                .HasMaxLength(200)
                .IsRequired();

            builder.HasIndex(s => s.Name)
                .IsUnique();

            // The lookup the webhook auth filter actually runs on every call — hash the
            // presented key, then find the (single) row whose KeyHash matches.
            builder.Property(s => s.KeyHash)
                .HasMaxLength(64) // SHA-256, hex-encoded, is always exactly 64 chars
                .IsRequired();

            builder.HasIndex(s => s.KeyHash)
                .IsUnique();

            builder.Property(s => s.IsActive)
                .IsRequired();

            builder.Property(s => s.LastUsedAt);

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt);
        }
    }
}
