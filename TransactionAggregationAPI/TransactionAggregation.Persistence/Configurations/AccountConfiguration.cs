using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Persistence.Configurations
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.ToTable("Accounts");

            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id)
                .HasConversion(
                    id => id.Value,
                    value => AccountId.CreateFrom(value));

            builder.Property(a => a.CustomerId)
                .HasConversion(
                    id => id.Value,
                    value => CustomerId.CreateFrom(value))
                .IsRequired();

            builder.Property(a => a.AccountNumber)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(a => a.AccountName)
                .HasMaxLength(200)
                .IsRequired();

            builder.Property(a => a.AccountType)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(a => a.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(a => a.Currency)
                .HasMaxLength(3)
                .IsRequired();

            builder.Property(a => a.IsActive)
                .IsRequired();

            builder.Property(a => a.CreatedAt)
                .IsRequired();

            builder.Property(a => a.UpdatedAt);

            // A customer cannot have duplicate account numbers
            builder.HasIndex(a => new { a.CustomerId, a.AccountNumber })
                .IsUnique()
                .HasDatabaseName("IX_Accounts_CustomerId_AccountNumber");

            builder.HasIndex(a => a.CustomerId)
                .HasDatabaseName("IX_Accounts_CustomerId");

            // Account -> Transaction (one account has many transactions)
            // The FK AccountId lives on the Transactions table and is nullable
            builder.HasMany(a => a.Transactions)
                .WithOne()
                .HasForeignKey("AccountId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
