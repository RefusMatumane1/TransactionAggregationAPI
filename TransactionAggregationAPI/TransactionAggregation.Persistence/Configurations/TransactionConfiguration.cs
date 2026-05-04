using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregation.Persistence.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions");
            
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => TransactionId.CreateFrom(value));

      
            builder.Property(t => t.CustomerId)
                .HasConversion(
                    id => id.Value,
                    value => CustomerId.CreateFrom(value))
                .IsRequired();

            builder.Property(t => t.AccountId)
                .HasConversion(
                    id => id != null ? (Guid?)id.Value : null,
                    value => value.HasValue ? AccountId.CreateFrom(value.Value) : null)
                .HasColumnName("AccountId");

            builder.HasIndex(t => t.AccountId)
                .HasDatabaseName("IX_Transactions_AccountId");

            builder.OwnsOne(t => t.Amount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("Amount")
                    .HasPrecision(18, 2)
                    .IsRequired();

                money.Property(m => m.Currency)
                    .HasColumnName("Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

       
            builder.OwnsOne(t => t.Source, source =>
            {
                source.Property(s => s.Name)
                    .HasColumnName("SourceName")
                    .HasMaxLength(50)
                    .IsRequired();

                source.Property(s => s.ExternalId)
                    .HasColumnName("SourceExternalId")
                    .HasMaxLength(100)
                    .IsRequired();

                source.Property(s => s.Provider)
                    .HasColumnName("SourceProvider")
                    .HasMaxLength(100);

                source.Property(s => s.Version)
                    .HasColumnName("SourceVersion")
                    .HasMaxLength(20);

                source.Property(s => s.LastSyncDate)
                    .HasColumnName("SourceLastSyncDate");

                source.HasIndex(s => s.ExternalId)
                    .HasDatabaseName("IX_Transactions_SourceExternalId");
            });

      
            builder.Property(t => t.Description)
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(t => t.Category)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(t => t.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(t => t.Date)
                .IsRequired();

            // Audit fields
            builder.Property(t => t.CreatedAt)
                .IsRequired();

            builder.Property(t => t.UpdatedAt);

            builder.Property(t => t.ApprovedAt);

            builder.Property(t => t.ApprovedBy)
                .HasMaxLength(100);

            // Metadata as JSON
            builder.Property(t => t.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new(),
                    new ValueComparer<Dictionary<string, string>>(
                        (c1, c2) => c1.SequenceEqual(c2),
                        c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => new Dictionary<string, string>(c)))
                .HasColumnType("jsonb");

    
            builder.HasIndex(t => t.CustomerId)
                .HasDatabaseName("IX_Transactions_CustomerId");

            builder.HasIndex(t => t.Date)
                .HasDatabaseName("IX_Transactions_Date");

            builder.HasIndex(t => t.Category)
                .HasDatabaseName("IX_Transactions_Category");

            builder.HasIndex(t => t.Status)
                .HasDatabaseName("IX_Transactions_Status");

            builder.HasIndex(t => new { t.CustomerId, t.Date, t.Category })
                .HasDatabaseName("IX_Transactions_Customer_Date_Category");

            builder.HasIndex(t => new { t.CustomerId, t.Status })
                .HasDatabaseName("IX_Transactions_Customer_Status");

            // Query filter for soft delete (if needed)
            // builder.HasQueryFilter(t => t.Status != TransactionStatus.Cancelled);
        }
    }
}
