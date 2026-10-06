using Modules.Transactions.Domain.Enums;

namespace TransactionAggregationAPI.Development
{
    internal static class SeedCatalog
    {
        public sealed record AccountDefinition(string ExternalAccountId, string Institution, SeedAccountType Type, int VariablePerMonth);

        public sealed record HouseholdDefinition(SeedProfile Profile, AccountDefinition[] Accounts);

        public static readonly HouseholdDefinition[] Households =
        [
            new(new(32000, 38000, false, 8500, 11000, 25, 1, ["Shoprite groceries", "Pick n Pay groceries"], ["Nandos dinner", "KFC meal"], []),
                [new("ZA0010000001", "FNB", SeedAccountType.Checking, 5), new("ZA0010000002", "FNB", SeedAccountType.Savings, 2)]),
            new(new(45000, 52000, false, 12000, 15000, 28, 1, ["Woolworths food", "Food Lovers Market"], ["Tashas restaurant", "Ocean Basket dinner"], [0, 1, 2]),
                [new("ZA0020000001", "StandardBank", SeedAccountType.Checking, 6), new("ZA0020000002", "Capitec", SeedAccountType.CreditCard, 7)]),
            new(new(58000, 68000, true, 14000, 18000, 25, 1, ["Woolworths food", "Pick n Pay groceries"], ["The Hussar Grill", "Mugg & Bean breakfast"], []),
                [new("ZA0030000001", "FNB", SeedAccountType.Checking, 5), new("ZA0030000002", "FNB", SeedAccountType.Savings, 2), new("ZA0030000003", "Absa", SeedAccountType.Investment, 2)]),
            new(new(18000, 24000, false, 5500, 7500, 25, 1, ["Shoprite groceries", "Checkers weekly shop"], ["Steers restaurant", "KFC meal"], []),
                [new("ZA0040000001", "StandardBank", SeedAccountType.Checking, 4)]),
            new(new(15000, 45000, false, 6000, 8000, 15, 3, ["Pick n Pay groceries", "Shoprite groceries"], ["Mugg & Bean breakfast", "Nandos dinner"], [0, 1, 4]),
                [new("ZA0050000001", "FNB", SeedAccountType.Savings, 2), new("ZA0050000002", "Absa", SeedAccountType.CreditCard, 8)]),
            new(new(26000, 31000, false, 7000, 9500, 25, 1, ["Shoprite groceries", "Checkers weekly shop"], ["Steers restaurant", "KFC meal"], []),
                [new("ZA0060000001", "StandardBank", SeedAccountType.Checking, 5), new("ZA0060000002", "StandardBank", SeedAccountType.Savings, 2)]),
            new(new(52000, 62000, true, 13000, 16000, 28, 1, ["Woolworths food", "Pick n Pay groceries"], ["The Hussar Grill", "Ocean Basket dinner"], []),
                [new("ZA0070000001", "FNB", SeedAccountType.Checking, 5), new("ZA0070000002", "Absa", SeedAccountType.Investment, 2)]),
            new(new(35000, 48000, false, 9000, 13000, 20, 3, ["Food Lovers Market", "Woolworths food"], ["Tashas restaurant", "Sushi King dinner"], [0, 2, 4]),
                [new("ZA0080000001", "StandardBank", SeedAccountType.Checking, 6), new("ZA0080000002", "Capitec", SeedAccountType.CreditCard, 8), new("ZA0080000003", "StandardBank", SeedAccountType.Savings, 3)]),
            new(new(15000, 20000, false, 4500, 6000, 25, 1, ["Shoprite groceries", "Checkers weekly shop"], ["KFC meal", "Steers restaurant"], []),
                [new("ZA0090000001", "FNB", SeedAccountType.Checking, 4)]),
            new(new(40000, 55000, false, 10000, 14000, 28, 1, ["Checkers weekly shop", "Food Lovers Market"], ["Mugg & Bean breakfast", "Ocean Basket dinner"], [1, 2, 3]),
                [new("ZA0100000001", "StandardBank", SeedAccountType.Checking, 5), new("ZA0100000002", "StandardBank", SeedAccountType.Savings, 2), new("ZA0100000003", "Capitec", SeedAccountType.CreditCard, 6)]),
        ];

        public static readonly (string Description, TransactionCategory Category, int Min, int Max, bool IsExpense)[] CheckingTemplates =
        [
            ("Shoprite groceries", TransactionCategory.Groceries, 300, 2500, true),
            ("Checkers weekly shop", TransactionCategory.Groceries, 400, 2200, true),
            ("Pick n Pay groceries", TransactionCategory.Groceries, 250, 1800, true),
            ("Woolworths food", TransactionCategory.Groceries, 500, 3000, true),
            ("Food Lovers Market", TransactionCategory.Groceries, 200, 1500, true),
            ("Nandos dinner", TransactionCategory.Dining, 120, 800, true),
            ("Steers restaurant", TransactionCategory.Dining, 80, 500, true),
            ("Mugg & Bean breakfast", TransactionCategory.Dining, 80, 300, true),
            ("Ocean Basket dinner", TransactionCategory.Dining, 200, 1200, true),
            ("KFC meal", TransactionCategory.Dining, 60, 250, true),
            ("Uber trip", TransactionCategory.Transportation, 50, 600, true),
            ("Bolt ride", TransactionCategory.Transportation, 40, 500, true),
            ("Shell fuel", TransactionCategory.Transportation, 300, 1200, true),
            ("BP petrol", TransactionCategory.Transportation, 250, 1000, true),
            ("Gautrain ticket", TransactionCategory.Transportation, 30, 200, true),
            ("Vodacom airtime", TransactionCategory.Utilities, 50, 500, true),
            ("MTN data bundle", TransactionCategory.Utilities, 49, 400, true),
            ("Telkom internet service", TransactionCategory.Utilities, 700, 1200, true),
            ("Johannesburg Water rates", TransactionCategory.Utilities, 200, 1500, true),
            ("Transfer to savings", TransactionCategory.Transfer, 500, 5000, true),
            ("EFT payment received", TransactionCategory.Transfer, 500, 2000, false),
            ("Freelance payment received", TransactionCategory.Income, 2000, 12000, false),
            ("Takealot online order", TransactionCategory.Shopping, 100, 3000, true),
            ("Mr Price clothing", TransactionCategory.Shopping, 100, 1500, true),
            ("Game electronics", TransactionCategory.Shopping, 200, 5000, true),
            ("Dis-Chem pharmacy", TransactionCategory.Healthcare, 50, 800, true),
            ("Clicks pharmacy", TransactionCategory.Healthcare, 50, 600, true),
        ];

        public static readonly (string Description, TransactionCategory Category, int Min, int Max, bool IsExpense)[] SavingsTemplates =
        [
            ("Transfer from cheque account", TransactionCategory.Transfer, 500, 8000, false),
            ("Monthly savings deposit", TransactionCategory.Income, 1000, 5000, false),
            ("Emergency fund contribution", TransactionCategory.Transfer, 500, 3000, false),
            ("Year-end savings top-up", TransactionCategory.Income, 2000, 10000, false),
            ("Withdrawal to cheque account", TransactionCategory.Transfer, 1000, 5000, true),
            ("Partial savings withdrawal", TransactionCategory.Transfer, 500, 3000, true),
        ];

        public static readonly (string Description, TransactionCategory Category, int Min, int Max)[] CreditCardTemplates =
        [
            ("Takealot online order", TransactionCategory.Shopping, 100, 3500),
            ("Woolworths clothing", TransactionCategory.Shopping, 200, 2500),
            ("Zara clothing", TransactionCategory.Shopping, 300, 2000),
            ("iStore purchase", TransactionCategory.Shopping, 500, 8000),
            ("H&M clothing", TransactionCategory.Shopping, 100, 1500),
            ("Vida e Caffe", TransactionCategory.Dining, 30, 200),
            ("Tashas restaurant", TransactionCategory.Dining, 200, 1200),
            ("Sushi King dinner", TransactionCategory.Dining, 150, 1000),
            ("The Hussar Grill", TransactionCategory.Dining, 300, 1500),
            ("Ster-Kinekor cinema", TransactionCategory.Entertainment, 80, 300),
            ("Nu Metro cinema", TransactionCategory.Entertainment, 80, 280),
            ("Dis-Chem pharmacy", TransactionCategory.Healthcare, 50, 800),
            ("Doctor consultation", TransactionCategory.Healthcare, 350, 1200),
            ("Dentist appointment", TransactionCategory.Healthcare, 500, 2500),
        ];

        public static readonly (string Description, TransactionCategory Category, int Amount)[] CreditCardSubscriptions =
        [
            ("Netflix subscription", TransactionCategory.Subscriptions, 199),
            ("Spotify premium", TransactionCategory.Subscriptions, 60),
            ("DStv subscription", TransactionCategory.Subscriptions, 699),
            ("Microsoft 365", TransactionCategory.Subscriptions, 149),
            ("Planet Fitness membership", TransactionCategory.Subscriptions, 399),
        ];

        public static readonly (string Description, TransactionCategory Category, int Min, int Max, bool IsExpense)[] InvestmentTemplates =
        [
            ("Dividend income", TransactionCategory.Income, 500, 5000, false),
            ("Unit trust interest", TransactionCategory.Income, 200, 3000, false),
            ("Portfolio quarterly return", TransactionCategory.Income, 1000, 8000, false),
            ("Lump sum investment", TransactionCategory.Transfer, 5000, 50000, false),
            ("Sanlam investment credit", TransactionCategory.Income, 500, 4000, false),
            ("Old Mutual return", TransactionCategory.Income, 800, 6000, false),
            ("Partial portfolio withdrawal", TransactionCategory.Transfer, 2000, 15000, true),
        ];
    }
}