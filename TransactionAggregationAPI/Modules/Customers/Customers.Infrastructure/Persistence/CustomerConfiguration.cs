using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Infrastructure.Persistence
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public const string AccountsTable = "CustomerAccounts";

        // Built CONCURRENTLY by the migration (indexes never block writes): the unique reference, and
        // the reverse lookup from a bank account to its customers.
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers", table =>
            {
                table.HasCheckConstraint("CK_Customers_Reference_Format", CodeFormat("Reference"));
                table.HasCheckConstraint("CK_Customers_Name_NotBlank", "btrim(\"Name\") <> ''");
            });

            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id)
                .HasConversion(id => id.Value, value => CustomerId.CreateFrom(value))
                .ValueGeneratedNever();

            builder.Property(c => c.Reference).HasMaxLength(Customer.MaxReferenceLength).IsRequired();
            builder.Property(c => c.Name).HasMaxLength(Customer.MaxNameLength).IsRequired();
            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.UpdatedAt);

            // A customer and its links are one aggregate; a link is identified by the account itself,
            // so linking the same account twice is impossible at the database level.
            builder.OwnsMany(c => c.Accounts, account =>
            {
                account.ToTable(AccountsTable, table =>
                {
                    table.HasCheckConstraint("CK_CustomerAccounts_Institution_Format", CodeFormat("Institution"));
                    table.HasCheckConstraint("CK_CustomerAccounts_ExternalAccountId_NotBlank", "btrim(\"ExternalAccountId\") <> ''");
                });
                account.WithOwner().HasForeignKey("CustomerId");
                account.Property<CustomerId>("CustomerId")
                    .HasConversion(id => id.Value, value => CustomerId.CreateFrom(value));
                account.HasKey("CustomerId", nameof(LinkedAccount.Institution), nameof(LinkedAccount.ExternalAccountId));

                account.Property(a => a.Institution).HasMaxLength(Customer.MaxInstitutionLength).IsRequired();
                account.Property(a => a.ExternalAccountId).HasMaxLength(Customer.MaxExternalAccountIdLength).IsRequired();
                account.Property(a => a.LinkedAt).IsRequired();
            });

            builder.Navigation(c => c.Accounts).HasField("_accounts").UsePropertyAccessMode(PropertyAccessMode.Field);
        }

        // The same rule the domain applies to references and bank codes.
        private static string CodeFormat(string column) => $"\"{column}\" ~ '^[A-Za-z0-9][A-Za-z0-9_-]*$'";
    }
}