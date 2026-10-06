using Microsoft.EntityFrameworkCore;
using Modules.Customers.Domain;
using Modules.Customers.Infrastructure.Persistence;

namespace TransactionAggregationAPI.Development
{
    // Development only: a customer for every seeded household, and three customers who hold the mock
    // aggregator's live-feed accounts across banks, so customer views have data from the first run.
    // Idempotent: existing customers keep their links, and missing links are added.
    internal static class CustomerSeed
    {
        // The mock aggregator's account ids (TransactionAggregation.MockAggregator/Catalog/MockCatalog.cs),
        // grouped into people who bank at several institutions.
        private static readonly (string Reference, string Name, (string Institution, string Account)[] Accounts)[] MockFeedCustomers =
        [
            ("CUST-0101", "Lerato Mokoena", [("FNB", "mock-fnb-chq-1001"), ("Absa", "mock-absa-cc-2002"), ("Capitec", "mock-capitec-sav-3002")]),
            ("CUST-0102", "Pieter van der Merwe", [("FNB", "mock-fnb-sav-1002"), ("Absa", "mock-absa-chq-2001"), ("StandardBank", "mock-sbsa-chq-4001")]),
            ("CUST-0103", "Ayesha Patel", [("FNB", "mock-fnb-chq-1003"), ("Capitec", "mock-capitec-chq-3001")]),
        ];

        private static readonly string[] HouseholdNames =
        [
            "Thandi Nkosi", "Johan Botha", "Naledi Dlamini", "Sipho Khumalo", "Fatima Adams",
            "Michael Naidoo", "Zanele Mthembu", "Ruan Pretorius", "Bongani Zulu", "Chantal Jacobs"
        ];

        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var now = DateTime.UtcNow;

            var wanted = SeedCatalog.Households
                .Select((household, i) => (
                    Reference: $"CUST-{i + 1:0000}",
                    Name: HouseholdNames[i % HouseholdNames.Length],
                    Accounts: household.Accounts.Select(a => (a.Institution, Account: a.ExternalAccountId)).ToArray()))
                .Concat(MockFeedCustomers)
                .ToList();

            var references = wanted.Select(w => w.Reference).ToList();
            var existing = await context.Customers
                .Where(c => references.Contains(c.Reference))
                .ToDictionaryAsync(c => c.Reference);

            var created = 0;
            var linked = 0;
            foreach (var (reference, name, accounts) in wanted)
            {
                if (!existing.TryGetValue(reference, out var customer))
                {
                    customer = Customer.Create(reference, name);
                    context.Customers.Add(customer);
                    created++;
                }

                foreach (var (institution, account) in accounts)
                {
                    if (customer.Link(institution, account, now))
                        linked++;
                }
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Customer seed: {Created} customers created, {Linked} accounts linked", created, linked);
        }
    }
}