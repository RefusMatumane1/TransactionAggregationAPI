using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using System.Text.Json;

namespace Modules.Transactions.Infrastructure.Persistence.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public const string CurrencyConstraintName = "CK_Transactions_Currency_Iso4217";
        public const string CurrencyConstraintSql = "\"Currency\" ~ '^[A-Z]{3}$'";

        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions", table =>
            {
                table.HasCheckConstraint("CK_Transactions_Amount_NonZero", "\"Amount\" <> 0");
                table.HasCheckConstraint(CurrencyConstraintName, CurrencyConstraintSql);
                table.HasCheckConstraint("CK_Transactions_Status_Defined", EnumRange<TransactionStatus>("Status"));
                table.HasCheckConstraint("CK_Transactions_Category_Defined", EnumRange<TransactionCategory>("Category"));
            });

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id)
                .HasConversion(
                    id => id.Value,
                    value => TransactionId.CreateFrom(value));

            builder.Property(t => t.ExternalAccountId)
                .HasMaxLength(Transaction.MaxExternalAccountIdLength)
                .IsRequired();

            builder.OwnsOne(t => t.Amount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("Amount")
                    .HasPrecision(19, Money.MaxScale)
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
                    .HasMaxLength(TransactionSource.MaxNameLength)
                    .IsRequired();

                source.Property(s => s.ExternalId)
                    .HasColumnName("SourceExternalId")
                    .HasMaxLength(TransactionSource.MaxExternalIdLength)
                    .IsRequired();
            });

            builder.Property(t => t.Description)
                .HasMaxLength(Transaction.MaxDescriptionLength)
                .IsRequired();

            builder.Property(t => t.Category)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(t => t.Status)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(t => t.Date)
                .IsRequired();

            builder.Property(t => t.CreatedAt)
                .IsRequired();

            // Legacy lifecycle values: kept, never written now.
            builder.Property<DateTime?>("UpdatedAt");

            builder.Ignore(t => t.Metadata);
            builder.Property<Dictionary<string, string>>("_metadata")
                .HasColumnName("Metadata")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, new JsonSerializerOptions()),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()) ?? new(),

                    new ValueComparer<Dictionary<string, string>>(
                        (c1, c2) => (c1 ?? new()).SequenceEqual(c2 ?? new()),
                        c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c == null ? new() : new Dictionary<string, string>(c)))
                .HasColumnType("jsonb");

            // Indexes are raw SQL in migrations (CONCURRENTLY, partial, INCLUDE, GIN): EF can't express them on owned
            // columns. SchemaContractTests pins them.
        }

        // Statuses with gaps must be listed, or the constraint admits retired ones.
        private static string EnumRange<TEnum>(string column) where TEnum : struct, Enum
        {
            var values = Enum.GetValues<TEnum>().Select(v => Convert.ToInt32(v)).Order().ToList();
            return values[^1] - values[0] == values.Count - 1
                ? $"\"{column}\" BETWEEN {values[0]} AND {values[^1]}"
                : $"\"{column}\" IN ({string.Join(", ", values)})";
        }
    }
}