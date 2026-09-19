using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.WebhookSources.ValueObjects;

namespace Modules.WebhookSources.Persistence
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

            builder.Property(s => s.KeyHash)
                            .HasMaxLength(64)
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
