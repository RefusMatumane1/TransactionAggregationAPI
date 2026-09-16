using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Persistence.Configurations
{
    public class BankLinkConfiguration : IEntityTypeConfiguration<BankLink>
    {
        public void Configure(EntityTypeBuilder<BankLink> builder)
        {
            builder.ToTable("BankLinks");

            builder.HasKey(b => b.Id);
            builder.Property(b => b.Id)
                .HasConversion(
                    id => id.Value,
                    value => BankLinkId.CreateFrom(value));

            builder.Property(b => b.CustomerId)
                .HasConversion(
                    id => id.Value,
                    value => CustomerId.CreateFrom(value))
                .IsRequired();

            builder.Property(b => b.AccountId)
                .HasConversion(
                    id => id != null ? (Guid?)id.Value : null,
                    value => value.HasValue ? AccountId.CreateFrom(value.Value) : null);

            builder.Property(b => b.Institution)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(b => b.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(b => b.ExternalAccountId)
                .HasMaxLength(200);

            // Encrypted (Data Protection ciphertext, base64) — sized generously since
            // protected payloads are larger than the raw token.
            builder.Property(b => b.EncryptedAccessToken)
                .HasMaxLength(4000);

            builder.Property(b => b.EncryptedRefreshToken)
                .HasMaxLength(4000);

            builder.Property(b => b.TokenExpiresAt);

            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.UpdatedAt);

            builder.HasIndex(b => b.CustomerId)
                .HasDatabaseName("IX_BankLinks_CustomerId");

            // A customer can only have one link per institution at a time (re-linking after
            // revocation reuses/replaces rather than accumulating duplicate rows).
            builder.HasIndex(b => new { b.CustomerId, b.Institution })
                .IsUnique()
                .HasDatabaseName("IX_BankLinks_CustomerId_Institution");
        }
    }
}
