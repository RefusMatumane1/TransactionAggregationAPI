using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;

namespace TransactionAggregationAPI;

public static class SeedData
{

    private record AccountDef(string Number, string Name, AccountType Type, int TxPerMonth);

    private record CustomerDef(string Email, string FullName, AccountDef[] Accounts);

    private record CustomerProfile(
    int SalaryMin, int SalaryMax,
    bool HasMortgage,
    int HousingMin, int HousingMax,
    int SalaryDay, int HousingDay,
    string[] GroceryStores,
    string[] DiningPlaces,
    string PrimarySource,
    int[] SubIndices);

    private static readonly CustomerDef[] CustomerDefs =
        [
            new("thabo.mokoena@example.co.za", "Thabo Mokoena",
        [
            new("ZA0010000001", "Thabo Cheque Account",      AccountType.Checking,   5),
            new("ZA0010000002", "Thabo Savings Account",     AccountType.Savings,    2),
        ]),

        new("lerato.dlamini@example.co.za", "Lerato Dlamini",
        [
            new("ZA0020000001", "Lerato Cheque Account",     AccountType.Checking,   6),
            new("ZA0020000002", "Lerato Credit Card",        AccountType.CreditCard, 7),
        ]),

        new("pieter.vandermerwe@example.co.za", "Pieter van der Merwe",
        [
            new("ZA0030000001", "Pieter Cheque Account",     AccountType.Checking,   5),
            new("ZA0030000002", "Pieter Savings Account",    AccountType.Savings,    2),
            new("ZA0030000003", "Pieter Investment Account", AccountType.Investment,  2),
        ]),

        new("nomvula.khumalo@example.co.za", "Nomvula Khumalo",
        [
            new("ZA0040000001", "Nomvula Cheque Account",    AccountType.Checking,   4),
        ]),

        new("sipho.ndlovu@example.co.za", "Sipho Ndlovu",
        [
            new("ZA0050000001", "Sipho Savings Account",     AccountType.Savings,    2),
            new("ZA0050000002", "Sipho Credit Card",         AccountType.CreditCard, 8),
        ]),

        new("zanele.motha@example.co.za", "Zanele Motha",
        [
            new("ZA0060000001", "Zanele Cheque Account",     AccountType.Checking,   5),
            new("ZA0060000002", "Zanele Savings Account",    AccountType.Savings,    2),
        ]),

        new("johan.botha@example.co.za", "Johan Botha",
        [
            new("ZA0070000001", "Johan Cheque Account",      AccountType.Checking,   5),
            new("ZA0070000002", "Johan Investment Account",  AccountType.Investment,  2),
        ]),

        new("ayanda.zulu@example.co.za", "Ayanda Zulu",
        [
            new("ZA0080000001", "Ayanda Cheque Account",     AccountType.Checking,   6),
            new("ZA0080000002", "Ayanda Credit Card",        AccountType.CreditCard, 8),
            new("ZA0080000003", "Ayanda Savings Account",    AccountType.Savings,    3),
        ]),

        new("mpho.sithole@example.co.za", "Mpho Sithole",
        [
            new("ZA0090000001", "Mpho Cheque Account",       AccountType.Checking,   4),
        ]),

        new("fatima.ismail@example.co.za", "Fatima Ismail",
        [
            new("ZA0100000001", "Fatima Cheque Account",     AccountType.Checking,   5),
            new("ZA0100000002", "Fatima Savings Account",    AccountType.Savings,    2),
            new("ZA0100000003", "Fatima Credit Card",        AccountType.CreditCard, 6),
        ]),
    ];

    private static readonly CustomerProfile[] Profiles =
        [

            new(32000, 38000, false,  8500, 11000, 25, 1,
            ["Shoprite groceries", "Pick n Pay groceries"],
            ["Nandos dinner", "KFC meal"],
            "BankA", []),

new(45000, 52000, false, 12000, 15000, 28, 1,
            ["Woolworths food", "Food Lovers Market"],
            ["Tashas restaurant", "Ocean Basket dinner"],
            "BankB", [0, 1, 2]),

new(58000, 68000, true,  14000, 18000, 25, 1,
            ["Woolworths food", "Pick n Pay groceries"],
            ["The Hussar Grill", "Mugg & Bean breakfast"],
            "BankA", []),

new(18000, 24000, false,  5500,  7500, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["Steers restaurant", "KFC meal"],
            "BankB", []),

new(15000, 45000, false,  6000,  8000, 15, 3,
            ["Pick n Pay groceries", "Shoprite groceries"],
            ["Mugg & Bean breakfast", "Nandos dinner"],
            "BankA", [0, 1, 4]),

new(26000, 31000, false,  7000,  9500, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["Steers restaurant", "KFC meal"],
            "BankB", []),

new(52000, 62000, true,  13000, 16000, 28, 1,
            ["Woolworths food", "Pick n Pay groceries"],
            ["The Hussar Grill", "Ocean Basket dinner"],
            "BankA", []),

new(35000, 48000, false,  9000, 13000, 20, 3,
            ["Food Lovers Market", "Woolworths food"],
            ["Tashas restaurant", "Sushi King dinner"],
            "BankB", [0, 2, 4]),

new(15000, 20000, false,  4500,  6000, 25, 1,
            ["Shoprite groceries", "Checkers weekly shop"],
            ["KFC meal", "Steers restaurant"],
            "BankA", []),

new(40000, 55000, false, 10000, 14000, 28, 1,
            ["Checkers weekly shop", "Food Lovers Market"],
            ["Mugg & Bean breakfast", "Ocean Basket dinner"],
            "BankB", [1, 2, 3]),
    ];

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

    private static readonly (string Desc, TransactionCategory Cat, int Amount)[]
            CreditCardSubscriptions =
            [
                ("Netflix subscription",      TransactionCategory.Subscriptions, 199),
            ("Spotify premium",           TransactionCategory.Subscriptions,  60),
            ("DStv subscription",         TransactionCategory.Subscriptions, 699),
            ("Microsoft 365",             TransactionCategory.Subscriptions, 149),
            ("Planet Fitness membership", TransactionCategory.Subscriptions, 399),
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

    public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminClient>();

        if (await context.Customers.AnyAsync()
            || await context.Accounts.AnyAsync()
            || await context.Transactions.AnyAsync())
            return;

        logger.LogInformation("Seeding database...");

        var rng = new Random(42);
        const string demoPassword = "Test@12345";

        var seedStart = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seedEnd = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var months = new List<DateTime>();
        for (var m = seedStart; m < seedEnd; m = m.AddMonths(1))
            months.Add(m);

        var customers = new List<Customer>();
        var accountWork = new List<(Account Account, AccountType Type, int TxPerMonth, int ProfileIndex)>();

        for (var ci = 0; ci < CustomerDefs.Length; ci++)
        {
            var def = CustomerDefs[ci];

            Guid keycloakUserId;
            try
            {
                keycloakUserId = await keycloak.CreateUserAsync(def.Email, def.FullName, demoPassword);
            }
            catch (KeycloakUserConflictException)
            {
                keycloakUserId = await keycloak.FindUserIdByEmailAsync(def.Email)
                    ?? throw new InvalidOperationException(
                        $"Keycloak reported '{def.Email}' as already existing but it could not be found by lookup.");
            }

            var customer = Customer.Create(CustomerId.CreateFrom(keycloakUserId), def.Email, def.FullName);

            foreach (var acct in def.Accounts)
            {
                var account = customer.AddAccount(acct.Number, acct.Name, acct.Type);
                accountWork.Add((account, acct.Type, acct.TxPerMonth, ci));
            }

            customers.Add(customer);
        }

        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        var transactions = new List<Transaction>();
        var seqNum = 0;

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
            var tx = MakeTx(acct, money, desc, cat, src, date);
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
            var p = Profiles[pi];
            var diningPool = p.DiningPlaces;
            var groceryPool = p.GroceryStores;

            foreach (var month in months)
            {
                var dim = DateTime.DaysInMonth(month.Year, month.Month);
                var isQEnd = month.Month is 3 or 6 or 9 or 12;

                if (type == AccountType.Checking)
                {

                    var salaryDay = Math.Min(p.SalaryDay, dim);
                    var salaryDate = new DateTime(month.Year, month.Month, salaryDay, 8, 0, 0, DateTimeKind.Utc);
                    var salary = (decimal)rng.Next(p.SalaryMin, p.SalaryMax + 1);
                    transactions.Add(MakeSettledTx(account, Money.Create(salary),
                        "Monthly salary", TransactionCategory.Income, p.PrimarySource, salaryDate));

                    var housingDay = Math.Min(p.HousingDay, dim);
                    var housingDate = new DateTime(month.Year, month.Month, housingDay, 9, 0, 0, DateTimeKind.Utc);
                    var housingAmt = -(decimal)rng.Next(p.HousingMin, p.HousingMax + 1);
                    var housingDesc = p.HasMortgage ? "Home loan instalment" : "Monthly rent payment";
                    transactions.Add(MakeSettledTx(account, Money.Create(housingAmt),
                        housingDesc, TransactionCategory.Housing, p.PrimarySource, housingDate));

                    transactions.Add(MakeSettledTx(account, Money.Create(-(decimal)rng.Next(400, 2800)),
                                            "Eskom electricity", TransactionCategory.Utilities, p.PrimarySource,
                                            new DateTime(month.Year, month.Month, Math.Min(15, dim), 7, 0, 0, DateTimeKind.Utc)));

                    transactions.Add(MakeSettledTx(account, Money.Create(-(decimal)rng.Next(300, 2200)),
                                            groceryPool[rng.Next(groceryPool.Length)], TransactionCategory.Groceries,
                                            p.PrimarySource, RandDate(month, 3, 10)));

                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax, isExp) = CheckingVariableTemplates[rng.Next(CheckingVariableTemplates.Length)];
                        var amount = isExp ? -(decimal)rng.Next(absMin, absMax + 1) : (decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 1, dim)));
                    }

                    if (rng.Next(10) < 6)
                    {
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(-(decimal)rng.Next(80, 900)),
                            diningPool[rng.Next(diningPool.Length)], TransactionCategory.Dining,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 10, 28)));
                    }
                }

                else if (type == AccountType.Savings)
                {

                    var depositDay = Math.Min(p.SalaryDay + 2, dim);
                    var depositDate = new DateTime(month.Year, month.Month, depositDay, 10, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(1000, 5000)),
                        "Transfer from cheque account", TransactionCategory.Transfer,
                        p.PrimarySource, depositDate));

                    var interestDate = new DateTime(month.Year, month.Month, dim, 23, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(50, 600)),
                        "Interest earned", TransactionCategory.Income, "Internal", interestDate));

                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax, isExp) = SavingsTemplates[rng.Next(SavingsTemplates.Length)];
                        var amount = isExp ? -(decimal)rng.Next(absMin, absMax + 1) : (decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            "Internal", RandDate(month, 1, dim)));
                    }
                }

                else if (type == AccountType.CreditCard)
                {

                    foreach (var si in p.SubIndices)
                    {
                        var (subDesc, subCat, subAmount) = CreditCardSubscriptions[si];
                        var subDate = new DateTime(month.Year, month.Month, 1,
                                                   rng.Next(0, 6), rng.Next(0, 60), 0, DateTimeKind.Utc);
                        transactions.Add(MakeSettledTx(account, Money.Create(-subAmount),
                            subDesc, subCat, "Internal", subDate));
                    }

                    var payDate = new DateTime(month.Year, month.Month, Math.Min(28, dim),
                                                                  7, 0, 0, DateTimeKind.Utc);
                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(2000, 10000)),
                        "Credit card payment", TransactionCategory.Transfer, p.PrimarySource, payDate));

                    for (var t = 0; t < txPerMonth; t++)
                    {
                        var (desc, cat, absMin, absMax) = CreditCardVariableTemplates[rng.Next(CreditCardVariableTemplates.Length)];
                        var amount = -(decimal)rng.Next(absMin, absMax + 1);
                        transactions.Add(MakeRandomStatusTx(account, Money.Create(amount), desc, cat,
                            Sources[rng.Next(Sources.Length)], RandDate(month, 1, dim)));
                    }
                }

                else if (type == AccountType.Investment)
                {

                    transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(2000, 10000)),
                        "Monthly investment contribution", TransactionCategory.Transfer,
                        p.PrimarySource,
                        new DateTime(month.Year, month.Month, 1, 9, 0, 0, DateTimeKind.Utc)));

                    if (isQEnd)
                    {
                        transactions.Add(MakeSettledTx(account, Money.Create((decimal)rng.Next(1000, 8000)),
                            "Portfolio quarterly return", TransactionCategory.Income,
                            p.PrimarySource,
                            new DateTime(month.Year, month.Month, dim, 12, 0, 0, DateTimeKind.Utc)));
                    }

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