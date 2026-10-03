namespace TransactionAggregation.MockAggregator.Catalog
{
    public sealed record MockAccount(
        string Id,
        string Institution,
        string AccountNumber,
        string AccountName,
        string AccountType,
        string Currency);

    public enum SpendKind
    {
        CardPurchase,
        DebitOrder,
        Income
    }

    public sealed record Merchant(
        string Name,
        string City,
        string Category,
        decimal MinAmount,
        decimal MaxAmount,
        SpendKind Kind);

    public static class MockCatalog
    {
        public static readonly IReadOnlyList<MockAccount> Accounts =
        [
            new("mock-fnb-chq-1001", "FNB", "62001001001", "FNB Gold Cheque", "checking", "ZAR"),
            new("mock-fnb-sav-1002", "FNB", "62001001002", "FNB Savings Pocket", "savings", "ZAR"),
            new("mock-fnb-chq-1003", "FNB", "62001001003", "FNB Easy Account", "checking", "ZAR"),
            new("mock-absa-chq-2001", "Absa", "40502002001", "Absa Gold Value Bundle", "checking", "ZAR"),
            new("mock-absa-cc-2002", "Absa", "40502002002", "Absa Credit Card", "credit_card", "ZAR"),
            new("mock-capitec-chq-3001", "Capitec", "13003003001", "Capitec Global One", "checking", "ZAR"),
            new("mock-capitec-sav-3002", "Capitec", "13003003002", "Capitec Savings Plan", "savings", "ZAR"),
            new("mock-sbsa-chq-4001", "StandardBank", "07004004001", "Standard Bank MyMo", "checking", "ZAR"),
        ];

        public static readonly IReadOnlyList<Merchant> Merchants =
        [
            new("Woolworths", "Sandton", "Groceries", 80, 1200, SpendKind.CardPurchase),
            new("Checkers", "Rosebank", "Groceries", 60, 1500, SpendKind.CardPurchase),
            new("Pick n Pay", "Menlyn", "Groceries", 50, 900, SpendKind.CardPurchase),
            new("Nandos", "Fourways", "Dining", 90, 450, SpendKind.CardPurchase),
            new("Mugg & Bean", "Cape Town", "Dining", 70, 380, SpendKind.CardPurchase),
            new("Engen", "Midrand", "Transportation", 300, 1100, SpendKind.CardPurchase),
            new("Uber", "Johannesburg", "Transportation", 45, 260, SpendKind.CardPurchase),
            new("Takealot", "Online", "Shopping", 150, 3500, SpendKind.CardPurchase),
            new("Dis-Chem", "Durban", "Healthcare", 60, 700, SpendKind.CardPurchase),
            new("Netflix", "Online", "Subscriptions", 99, 199, SpendKind.DebitOrder),
            new("Discovery Health", "Sandton", "Healthcare", 1800, 4200, SpendKind.DebitOrder),
            new("City Power", "Johannesburg", "Utilities", 400, 2600, SpendKind.DebitOrder),
            new("Acme Holdings Salary", "Johannesburg", "Income", 18000, 42000, SpendKind.Income),
        ];
    }
}