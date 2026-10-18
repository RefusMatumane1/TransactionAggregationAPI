using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Notifications.Domain;

namespace Modules.Notifications.Infrastructure.Persistence
{
    public class AlertWebhookConfiguration : IEntityTypeConfiguration<AlertWebhook>
    {
        public void Configure(EntityTypeBuilder<AlertWebhook> builder)
        {
            builder.ToTable("AlertWebhooks");

            // One destination per bank: the bank code is the key, so a second row for a bank cannot exist.
            builder.HasKey(w => w.BankCode);

            builder.Property(w => w.BankCode)
                .HasMaxLength(AlertWebhook.MaxBankCodeLength)
                .ValueGeneratedNever();

            builder.Property(w => w.ProtectedUrl)
                .HasMaxLength(AlertWebhook.MaximumProtectedUrlLength)
                .IsRequired();

            builder.Property(w => w.UpdatedAt).IsRequired();
        }
    }
}