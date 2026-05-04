using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;

namespace TransactionAggregationAPI;

public static class SeedData
{
    // ── Definitions ──────────────────────────────────────────────────────────

    // TxPerMonth = variable transactions added on top of the guaranteed monthly anchors
    private record AccountDef(string Number, string Name, AccountType Type, int TxPerMonth);

    private record CustomerDef(string Id, string Email, string FullName, AccountDef[] Accounts);

    /// <summary>
    /// Per-customer spending profile. Index must match CustomerDefs.
    /// SubIndices: which entries from CreditCardSubscriptions this customer pays each month.
    /// </summary>
    private record CustomerProfile(
        int SalaryMin,       int SalaryMax,
        bool HasMortgage,
        int HousingMin,      int HousingMax,
        int SalaryDay,       int HousingDay,
        string[] GroceryStores,
        string[] DiningPlaces,
        string PrimarySource,
        int[] SubIndices);

    // ── Customer definitions ──────────────────────────────────────────────────

    private static readonly CustomerDef[] CustomerDefs =
    [
        new("e13ffb3d-ea72-45f6-b5a6-5da4c65eeb50", "thabo.mokoena@example.co.za", "Thabo Mokoena",
        [
            new("ZA0010000001", "Thabo Cheque Account",      AccountType.Checking,   5),
            new("ZA0010000002", "Thabo Savings Account",     AccountType.Savings,    2),
        ]),

        new("39af1512-f86d-4ae7-b68a-9664a6cd5d5b", "lerato.dlamini@example.co.za", "Lerato Dlamini",
        [
            new("ZA0020000001", "Lerato Cheque Account",     AccountType.Checking,   6),
            new("ZA0020000002", "Lerato Credit Card",        AccountType.CreditCard, 7),
        ]),

        new("ed3402d5-a33f-4d04-96c0-c0d76b5615b4", "pieter.vandermerwe@example.co.za", "Pieter van der Merwe",
        [
            new("ZA0030000001", "Pieter Cheque Account",     AccountType.Checking,   5),
            new("ZA0030000002", "Pieter Savings Account",    AccountType.Savings,    2),
            new("ZA0030000003", "Pieter Investment Account", AccountType.Investment,  2),
        ]),

        new("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "nomvula.khumalo@example.co.za", "Nomvula Khumalo",
        [
            new("ZA0040000001", "Nomvula Cheque Account",    AccountType.Checking,   4),
        ]),

        new("b2c3d4e5-f6a7-8901-bcde-f12345678901", "sipho.ndlovu@example.co.za", "Sipho Ndlovu",
        [
            new("ZA0050000001", "Sipho Savings Account",     AccountType.Savings,    2),
            new("ZA0050000002", "Sipho Credit Card",         AccountType.CreditCard, 8),
        ]),

        new("c3d4e5f6-a7b8-9012-cdef-123456789012", "zanele.motha@example.co.za", "Zanele Motha",
        [
            new("ZA0060000001", "Zanele Cheque Account",     AccountType.Checking,   5),
            new("ZA0060000002", "Zanele Savings Account",    AccountType.Savings,    2),
        ]),

        new("d4e5f6a7-b8c9-0123-def0-234567890123", "johan.botha@example.co.za", "Johan Botha",
        [
            new("ZA0070000001", "Johan Cheque Account",      AccountType.Checking,   5),
            new("ZA0070000002", "Johan Investment Account",  AccountType.Investment,  2),
        ]),

        new("e5f6a7b8-c9d0-1234-ef01-345678901234", "ayanda.zulu@example.co.za", "Ayanda Zulu",
        [
            new("ZA0080000001", "Ayanda Cheque Account",     AccountType.Checking,   6),
            new("ZA0080000002", "Ayanda Credit Card",        AccountType.CreditCard, 8),
            new("ZA0080000003", "Ayanda Savings Account",    AccountType.Savings,    3),
        ]),

        new("f6a7b8c9-d0e1-2345-f012-456789012345", "mpho.sithole@example.co.za", "Mpho Sithole",
        [
            new("ZA0090000001", "Mpho Cheque Account",       AccountType.Checking,   4),
        ]),

        new("a7b8c9d0-e1f2-3456-0123-567890123456", "fatima.ismail@example.co.za", "Fatima Ismail",
        [
            new("ZA0100000001", "Fatima Cheque Account",     AccountType.Checking,   5),
            new("ZA0100000002", "Fatima Savings Account",    AccountType.Savings,    2),
            new("ZA0100000003", "Fatima Credit Card",        AccountType.CreditCard, 6),
        ]),
    ];

    // ── Per-customer profiles (index matches CustomerDefs) ────────────────────

    private static readonly CustomerProfile[] Profiles =
    [
        // 0 — Thabo: middle class, rents, budget-moderate
        new(32000, 38000, false,  8500, 11000, 25, 1,
            ["Shoprite groceries", "Pick n Pay groceries"],
            ["Nandos dinner", "KFC meal"],
            "BankA", []),

        // 1 — Lerato: professional, rents, heavy diner & shopper
        new(45000, 52000, false, 12000, 15000, 28, 1,
            ["Woolworths food", "Food Lovers Market"],
            ["Tashas restaurant", "Ocean Basket dinner"],
            "BankB", [0, 1, 2]),          // Netflix, Spotify, DStv

        // 2 — Pieter: high income, mortgage, investor
        new(58000, 68000, true,  14000, 18000, 25, 1,
            ["Woolworths food", "Pick n Pay groceries"],
            ["The Hussar Grill", "Mugg & Bean breakfast"],
            "BankA", []),

        // 3 — Nomvula: entry level, rents, very budget-conscious
        new(18000, 24000, false,  5500,  7500, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["Steers restaurant", "KFC meal"],
            "BankB", []),

        // 4 — Sipho: freelancer (variable income), credit-card-heavy
        new(15000, 45000, false,  6000,  8000, 15, 3,
            ["Pick n Pay groceries", "Shoprite groceries"],
            ["Mugg & Bean breakfast", "Nandos dinner"],
            "BankA", [0, 1, 4]),          // Netflix, Spotify, Planet Fitness

        // 5 — Zanele: working class, rents, practical spender
        new(26000, 31000, false,  7000,  9500, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["Steers restaurant", "KFC meal"],
            "BankB", []),

        // 6 — Johan: senior professional, mortgage, investor
        new(52000, 62000, true,  13000, 16000, 28, 1,
            ["Woolworths food", "Pick n Pay groceries"],
            ["The Hussar Grill", "Ocean Basket dinner"],
            "BankA", []),

        // 7 — Ayanda: entrepreneur, variable income, lifestyle spender
        new(35000, 48000, false,  9000, 13000, 20, 3,
            ["Food Lovers Market", "Woolworths food"],
            ["Tashas restaurant", "Sushi King dinner"],
            "BankB", [0, 2, 4]),          // Netflix, DStv, Planet Fitness

        // 8 — Mpho: junior/graduate, minimal spending
        new(15000, 20000, false,  4500,  6000, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["KFC meal", "Steers restaurant"],
            "BankA", []),

        // 9 — Fatima: small-business owner, mixed income
        new(40000, 55000, false, 10000, 14000, 28, 1,
            ["Checkers weekly shop", "Food Lovers Market"],
            ["Mugg & Bean breakfast", "Ocean Basket dinner"],
            "BankB", [1, 2, 3]),          // Spotify, DStv, Microsoft 365
    ];

    // ── Transaction templates ─────────────────────────────────────────────────

    private static readonly (string Desc, TransactionCategory Cat, int AbsMin, int AbsMax, bool IsExpense)[]
        CheckingVariableTemplates =
        [
            ("Shoprite groceries",         TransactionCategory.Groceries,         300,  2500, true),
            ("Checkers weekly shop",       TransactionCategory.Groceries,         400,  2200, true),
            ("Pick n Pay groceries",       TransactionCategory.Groceries,         250,  1800, true),
            ("Woolworths food",            TransactionCategory.Groceries,         500,  3000, true),
            ("Food Lovers Market",         TransactionCategory.Groceries,         200,  1500, true),
            ("Nandos dinner",              TransactionCategory.Dining,            120,   800, true),
            ("Steers restaurant",          TransactionCategory.Dining,             80,   500, true),
            ("Mugg & Bean breakfast",      TransactionCategory.Dining,             80,   300, true),
            ("Ocean Basket dinner",        TransactionCategory.Dining,            200,  1200, true),
            ("KFC meal",                   TransactionCategory.Dining,             60,   250, true),
            ("Uber trip",                  TransactionCategory.Transportation,     50,   600, true),
            ("Bolt ride",                  TransactionCategory.Transportation,     40,   500, true),
            ("Shell fuel",                 TransactionCategory.Transportation,    300,  1200, true),
            ("BP petrol",                  TransactionCategory.Transportation,    250,  1000, true),
            ("Gautrain ticket",            TransactionCategory.Transportation,     30,   200, true),
            ("Vodacom airtime",            TransactionCategory.Utilities,          50,   500, true),
            ("MTN data bundle",            TransactionCategory.Utilities,          49,   400, true),
            ("Telkom internet service",    TransactionCategory.Utilities,         700,  1200, true),
            ("Johannesburg Water rates",   TransactionCategory.Utilities,         200,  1500, true),
            ("Transfer to savings",        TransactionCategory.Transfer,          500,  5000, true),
            ("EFT payment received",       TransactionCategory.Transfer,          500,  2000, false),
            ("Freelance payment received", TransactionCategory.Income,           2000, 12000, false),
            ("Takealot online order",      TransactionCategory.Shopping,          100,  3000, true),
            ("Mr Price clothing",          TransactionCategory.Shopping,          100,  1500, true),
            ("Game electronics",           TransactionCategory.Shopping,          200,  5000, true),
            ("Dis-Chem pharmacy",          TransactionCategory.Healthcare,         50,   800, true),
            ("Clicks pharmacy",            TransactionCategory.Healthcare,         50,   600, true),
        ];

    private static readonly (string Desc, TransactionCategory Cat, int AbsMin, int AbsMax, bool IsExpense)[]
        SavingsTemplates =
        [
            ("Transfer from cheque account", TransactionCategory.Transfer,   500,  8000, false),
            ("Monthly savings deposit",      TransactionCategory.Income,    1000,  5000, false),
            ("Emergency fund contribution",  TransactionCategory.Transfer,   500,  3000, false),
            ("Year-end savings top-up",      TransactionCategory.Income,    2000, 10000, false),
            ("Withdrawal to cheque account", TransactionCategory.Transfer,  1000,  5000, true),
            ("Partial savings withdrawal",   TransactionCategory.Transfer,   500,  3000, true),
        ];

    // Credit card variable purchases (always expenses)
    private static readonly (string Desc, TransactionCategory Cat, int AbsMin, int AbsMax)[]
        CreditCardVariableTemplates =
        [
            ("Takealot online order",    TransactionCategory.Shopping,       100,  3500),
            ("Woolworths clothing",      TransactionCategory.Shopping,       200,  2500),
            ("Zara clothing",            TransactionCategory.Shopping,       300,  2000),
            ("iStore purchase",          TransactionCategory.Shopping,       500,  8000),
            ("H&M clothing",             TransactionCategory.Shopping,       100,  1500),
            ("Vida e Caffe",             TransactionCategory.Dining,          30,   200),
            ("Tashas restaurant",        TransactionCategory.Dining,         200,  1200),
            ("Sushi King dinner",        TransactionCategory.Dining,         150,  1000),
            ("The Hussar Grill",         TransactionCategory.Dining,         300,  1500),
            ("Ster-Kinekor cinema",      TransactionCategory.Entertainment,   80,   300),
            ("Nu Metro cinema",          TransactionCategory.Entertainment,   80,   280),
            ("Dis-Chem pharmacy",        TransactionCategory.Healthcare,      50,   800),
            ("Doctor consultation",      TransactionCategory.Healthcare,     350,  1200),
            ("Dentist appointment",      TransactionCategory.Healthcare,     500,  2500),
        ];

    // Fixed monthly subscriptions — index into this array is used by CustomerProfile.SubIndices
    private static readonly (string Desc, TransactionCategory Cat, int Amount)[]
        CreditCardSubscriptions =
        [
            ("Netflix subscription",      TransactionCategory.Subscriptions, 199),   // 0
            ("Spotify premium",           TransactionCategory.Subscriptions,  60),   // 1
            ("DStv subscription",         TransactionCategory.Subscriptions, 699),   // 2
            ("Microsoft 365",             TransactionCategory.Subscriptions, 149),   // 3
            ("Planet Fitness membership", TransactionCategory.Subscriptions, 399),   // 4
        ];

    private static readonly (string Desc, TransactionCategory Cat, int AbsMin, int AbsMax, bool IsExpense)[]
        InvestmentTemplates =
        [
            ("Dividend income",                 TransactionCategory.Income,     500,  5000, false),
            ("Unit trust interest",             TransactionCategory.Income,     200,  3000, false),
            ("Portfolio quarterly return",      TransactionCategory.Income,    1000,  8000, false),
            ("Lump sum investment",             TransactionCategory.Transfer,  5000, 50000, false),
            ("Sanlam investment credit",        TransactionCategory.Income,     500,  4000, false),
            ("Old Mutual return",               TransactionCategory.Income,     800,  6000, false),
            ("Partial portfolio withdrawal",    TransactionCategory.Transfer,  2000, 15000, true),
        ];

    // Weighted status pool: 50% Settled, some Approved/Pending, few edge cases
    private static readonly TransactionStatus[] StatusPool =
    [
        TransactionStatus.Settled,  TransactionStatus.Settled,  TransactionStatus.Settled,
        TransactionStatus.Settled,  TransactionStatus.Settled,  TransactionStatus.Settled,
        TransactionStatus.Approved, TransactionStatus.Approved,
        TransactionStatus.Pending,
        TransactionStatus.Rejected,
        TransactionStatus.Flagged,
        TransactionStatus.Cancelled,
    ];

    private static readonly string[] Sources = ["BankA", "BankB", "Internal"];

    // ── Entry point ───────────────────────────────────────────────────────────

    public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope  = serviceProvider.CreateScope();
        var context      = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger       = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var hasher       = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await context.Customers.AnyAsync()
            || await context.Accounts.AnyAsync()
            || await context.Transactions.AnyAsync())
            return;

        logger.LogInformation("Seeding database...");

        var rng      = new Random(42);
        var password = hasher.Hash("Test@12345");

        // ── Seed window: Jan 2025 → Apr 2026 (16 months) ─────────────────────
        // Covers a complete "last year" (2025) and "this year to date" (2026),
        // so every date-range filter the UI can produce returns results.
        var seedStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seedEnd   = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc); // exclusive (today)
        var months    = new List<DateTime>();
        for (var m = seedStart; m < seedEnd; m = m.AddMonths(1))
            months.Add(m);

        // ── Build customers + accounts ────────────────────────────────────────
        var customers   = new List<Customer>();
        var accountWork = new List<(Account Account, AccountType Type, int TxPerMonth, int ProfileIndex)>();

        for (var ci = 0; ci < CustomerDefs.Length; ci++)
        {
            var def      = CustomerDefs[ci];
            var customer = Customer.Create(
                CustomerId.CreateFrom(Guid.Parse(def.Id)),
                def.Email, def.FullName, password);

            foreach (var acct in def.Accounts)
            {
                var account = customer.AddAccount(acct.Number, acct.Name, acct.Type);
                accountWork.Add((account, acct.Type, acct.TxPerMonth, ci));
            }

            customers.Add(customer);
        }

        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        // ── Build transactions month-by-month ─────────────────────────────────
        var transactions = new List<Transaction>();
        var seqNum       = 0;

        // Local helpers — capture rng and seqNum via closure (avoids ref in async method)
        Transaction MakeTx(Account acct, Money money, string desc, TransactionCategory cat,
                           string src, DateTime date)
        {
            var source = TransactionSource.Create(src, $"seed-{acct.Id.Value:N}-{seqNum:D6}");
            seqNum++;
            return Transaction.Create(acct.CustomerId, money, desc, cat, source, acct.Id, date);
        }

        Transaction MakeSettledTx(Account acct, Money money, string desc, TransactionCategory cat,
                                  string src, DateTime date)
        {
            var tx = MakeTx(acct, money, desc, cat, src, date);
            tx.UpdateStatus(TransactionStatus.Settled, "Monthly recurring");
            return tx;
        }

        Transaction MakeRandomStatusTx(Account acct, Money money, string desc, TransactionCategory cat,
                                       string src, DateTime date)
        {
            var tx     = MakeTx(acct, money, desc, cat, src, date);
            var status = StatusPool[rng.Next(StatusPool.Length)];
            if (status != TransactionStatus.Pending)
                tx.UpdateStatus(status, "Seed data");
            return tx;
        }

        DateTime RandDate(DateTime month, int dayMin, int dayMax)
        {
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var day = rng.Next(Math.Max(1, dayMin), Math.Min(daysInMonth, dayMax) + 1);
            return new DateTime(month.Year, month.Month, day,
                                rng.Next(7, 22), rng.Next(0, 60), 0, DateTimeKind.Utc);
        }

        foreach (var (account, type, txPerMonth, pi) in accountWork)
        {
            var p           = Profiles[pi];
            var diningPool  = p.DiningPlaces;
            var groceryPool = p.GroceryStores;

            foreach (var month in months)
            {
                var dim       = DateTime.DaysInMonth(month.Year, month.Month);
                var isQEnd    = month.Month is 3 or 6 or 9 or 12;

                // ── Checking ──────────────────────────────────────────────────
                if (type == AccountType.Checking)
                {
                    // 1 — Salary (always on salary day, Settled)
                    var salaryDay  = Math.Min(p.SalaryDay, dim);
                    var salaryDate = new DateTime(month.Year, month.Month, salaryDay, 8, 0, 0, DateTimeKind.Utc);
                    var salary     = (decimal)rng.Next(p.SalaryMin, p.SalaryMax + 1);
                    transactions.Add(MakeSettledTx(account, Money.Create(salary),
                        "Monthly salary", TransactionCategory.Income, p.PrimarySource, salaryDate));

                    // 2 — Housing (rent or mortgage, Settled)
                    var housingDay  = Math.Min(p.HousingDay, dim);
                    var housingDate = new DateTime(month.Year, month.Month, housingDay, 9, 0, 0, DateTimeKind.Utc);
                    var housingAmt  = -(decimal)rng.Next(p.HousingMin, p.HousingMax + 1);
                    var housingDesc = p.HasMortgage ? "Home loan instalment" : "Monthly rent payment";
                    transactions.Add(MakeSettledTx(account, Money.Create(housingAmt),
                        housingDesc, TransactionCategory.Housing, p.PrimarySource, housingDate));

                    // 3 — Electricity (Settled, mid-month)
                    transactions.Add(MakeSettledTx(account, Money.Create(-(decimal)rng.Next(400, 2800)),
                        "Eskom electricity", TransactionCategory.Utilities, p.PrimarySource,
                        new DateTime(month.Year, month.Month, Math.Min(15, dim), 7, 0, 0, DateTimeKind.Utc)));

                    // 4 — Grocery shop (customer's preferred store, Settled)
                    transactions.Add(MakeSettledTx(account, Money.Create(-(decimal)rng.Next(300, 2200)),
                        groceryPool[rng.Next(groceryPool.Length)], TransactionCategory.Groceries,
                        p.PrimarySource, RandDate(month, 3, 10)));

                    // 5 — Variable transactions for the month
                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax, isExp) = CheckingVariableTemplates[rng.Next(CheckingVariableTemplates.Length)];
                        var amount = isExp ? -(decimal)rng.Next(absMin, absMax + 1) : (decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 1, dim)));
                    }

                    // 6 — Occasional dining (60% of months, customer's preferred place)
                    if (rng.Next(10) < 6)
                    {
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(-(decimal)rng.Next(80, 900)),
                            diningPool[rng.Next(diningPool.Length)], TransactionCategory.Dining,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 10, 28)));
                    }
                }

                // ── Savings ───────────────────────────────────────────────────
                else if (type == AccountType.Savings)
                {
                    // 1 — Monthly savings transfer (2-3 days after salary day, Settled)
                    var depositDay  = Math.Min(p.SalaryDay + 2, dim);
                    var depositDate = new DateTime(month.Year, month.Month, depositDay, 10, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(1000, 5000)),
                        "Transfer from cheque account", TransactionCategory.Transfer,
                        p.PrimarySource, depositDate));

                    // 2 — Interest (last day of month, Settled)
                    var interestDate = new DateTime(month.Year, month.Month, dim, 23, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(50, 600)),
                        "Interest earned", TransactionCategory.Income, "Internal", interestDate));

                    // 3 — Variable (occasional extra deposits or withdrawals)
                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax, isExp) = SavingsTemplates[rng.Next(SavingsTemplates.Length)];
                        var amount = isExp ? -(decimal)rng.Next(absMin, absMax + 1) : (decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            "Internal", RandDate(month, 1, dim)));
                    }
                }

                // ── Credit card ───────────────────────────────────────────────
                else if (type == AccountType.CreditCard)
                {
                    // 1 — Fixed monthly subscriptions on the 1st (Settled)
                    foreach (var si in p.SubIndices)
                    {
                        var (subDesc, subCat, subAmount) = CreditCardSubscriptions[si];
                        var subDate = new DateTime(month.Year, month.Month, 1,
                                                   rng.Next(0, 6), rng.Next(0, 60), 0, DateTimeKind.Utc);
                        transactions.Add(MakeSettledTx(account, Money.Create(-subAmount),
                            subDesc, subCat, "Internal", subDate));
                    }

                    // 2 — Monthly credit card payment (Settled, near end of month)
                    var payDate = new DateTime(month.Year, month.Month, Math.Min(28, dim),
                                              7, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(2000, 10000)),
                        "Credit card payment", TransactionCategory.Transfer, p.PrimarySource, payDate));

                    // 3 — Variable purchases (shopping, dining, entertainment, health)
                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax) = CreditCardVariableTemplates[rng.Next(CreditCardVariableTemplates.Length)];
                        var amount = -(decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 1, dim)));
                    }
                }

                // ── Investment ────────────────────────────────────────────────
                else if (type == AccountType.Investment)
                {
                    // 1 — Monthly contribution (Settled, start of month)
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(2000, 10000)),
                        "Monthly investment contribution", TransactionCategory.Transfer,
                        p.PrimarySource,
                        new DateTime(month.Year, month.Month, 1, 9, 0, 0, DateTimeKind.Utc)));

                    // 2 — Quarterly return (end of Mar, Jun, Sep, Dec — Settled)
                    if (isQEnd)
                    {
                        transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(1000, 8000)),
                            "Portfolio quarterly return", TransactionCategory.Income,
                            p.PrimarySource,
                            new DateTime(month.Year, month.Month, dim, 12, 0, 0, DateTimeKind.Utc)));
                    }

                    // 3 — Variable (dividends, top-ups, occasional withdrawals)
                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax, isExp) = InvestmentTemplates[rng.Next(InvestmentTemplates.Length)];
                        var amount = isExp ? -(decimal)rng.Next(absMin, absMax + 1) : (decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            p.PrimarySource, RandDate(month, 1, dim)));
                    }
                }
            }
        }

        await context.Transactions.AddRangeAsync(transactions);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded {Customers} customers, {Accounts} accounts, {Transactions} transactions across {Months} months ({Start:yyyy-MM} → {End:yyyy-MM})",
            customers.Count, accountWork.Count, transactions.Count, months.Count,
            months.First(), months.Last());
    }
}
