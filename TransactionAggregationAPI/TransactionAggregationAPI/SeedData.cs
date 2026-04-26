using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;

namespace TransactionAggregationAPI
{
    public static class SeedData
    {
        public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            if (!await context.Customers.AnyAsync())
            {
                logger.LogInformation("Seeding database...");

                var customers = new[]
                {
                Customer.Create(CustomerId.Create(), "john.doe@example.com", "John Doe"),
                Customer.Create(CustomerId.Create(), "jane.smith@example.com", "Jane Smith"),
                Customer.Create(CustomerId.Create(), "bob.wilson@example.com", "Bob Wilson")
            };

                await context.Customers.AddRangeAsync(customers);
                await context.SaveChangesAsync();

                // Add sample transactions
                var random = new Random();
                foreach (var customer in customers)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var amount = random.Next(-500, 1000);
                        var transaction = Transaction.Create(
                            customer.Id,
                            Money.Create(amount, "ZAR"),
                             $"Sample transaction {i + 1}",
                             TransactionAggregation.Domain.Enums.TransactionCategory.Uncategorized,
                            TransactionSource.Create("SeedData", Guid.NewGuid().ToString())
                            );

                        transaction.UpdateStatus(TransactionStatus.Settled, "Seed data");
                        await context.Transactions.AddAsync(transaction);
                    }
                }

                await context.SaveChangesAsync();
                logger.LogInformation("Database seeded successfully!");
            }
        }
    }
}
