using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;

namespace TransactionAggregationAPI.Development
{
    internal sealed record SeedProfile(
        int SalaryMin, int SalaryMax,
        bool HasMortgage,
        int HousingMin, int HousingMax,
        int SalaryDay, int HousingDay,
        string[] GroceryStores,
        string[] DiningPlaces,
        int[] SubscriptionIndices);

    public enum SeedAccountType
    {
        Checking,
        Savings,
        CreditCard,
        Investment
    }

    internal sealed record SeedAccount(
        string ExternalAccountId, string Institution, SeedAccountType Type, int VariablePerMonth, SeedProfile Profile);

    internal sealed class SeedTransactionGenerator(Random rng, Guid runId, DateTime nowUtc)
    {
        public const int HistoryMonths = 15;
        public static readonly TimeSpan RecentActivityWindow = TimeSpan.FromHours(48);

        private int _sequence;

        public IReadOnlyList<Transaction> Generate(SeedAccount account, DateTime? lastSeededUtc)
        {
            var from = lastSeededUtc ?? StartOfMonth(nowUtc).AddMonths(-(HistoryMonths - 1)).AddTicks(-1);
            var transactions = new List<Transaction>();

            void Add(Transaction transaction)
            {
                if (transaction.Date > from && transaction.Date <= nowUtc)
                    transactions.Add(transaction);
            }

            for (var month = StartOfMonth(from.AddTicks(1)); month <= nowUtc; month = month.AddMonths(1))
                GenerateMonth(account, month, Add);

            var recentFrom = Max(from, nowUtc - RecentActivityWindow);
            var recentCount = rng.Next(2, 6);
            for (var i = 0; i < recentCount; i++)
            {
                var date = recentFrom + TimeSpan.FromSeconds(rng.NextDouble() * (nowUtc - recentFrom).TotalSeconds);
                Add(Variable(account, DateTime.SpecifyKind(date, DateTimeKind.Utc)));
            }

            return transactions;
        }

        private void GenerateMonth(SeedAccount account, DateTime month, Action<Transaction> add)
        {
            var p = account.Profile;
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

            switch (account.Type)
            {
                case SeedAccountType.Checking:
                    add(Booked(account, rng.Next(p.SalaryMin, p.SalaryMax + 1), "Monthly salary", TransactionCategory.Income,
                        account.Institution, OnDay(month, p.SalaryDay, 8)));
                    add(Booked(account, -rng.Next(p.HousingMin, p.HousingMax + 1),
                        p.HasMortgage ? "Home loan instalment" : "Monthly rent payment", TransactionCategory.Housing,
                        account.Institution, OnDay(month, p.HousingDay, 9)));
                    add(Booked(account, -rng.Next(400, 2800), "Eskom electricity", TransactionCategory.Utilities,
                        account.Institution, OnDay(month, 15, 7)));
                    add(Booked(account, -rng.Next(300, 2200), Pick(p.GroceryStores), TransactionCategory.Groceries,
                        account.Institution, RandomDay(month, 3, 10)));
                    if (rng.Next(10) < 6)
                        add(Booked(account, -rng.Next(80, 900), Pick(p.DiningPlaces), TransactionCategory.Dining,
                            account.Institution, RandomDay(month, 10, 28)));
                    break;

                case SeedAccountType.Savings:
                    add(Booked(account, rng.Next(1000, 5000), "Transfer from cheque account", TransactionCategory.Transfer,
                        account.Institution, OnDay(month, p.SalaryDay + 2, 10)));
                    add(Booked(account, rng.Next(50, 600), "Interest earned", TransactionCategory.Income,
                        account.Institution, OnDay(month, daysInMonth, 23)));
                    break;

                case SeedAccountType.CreditCard:
                    foreach (var index in p.SubscriptionIndices)
                    {
                        var (description, category, amount) = SeedCatalog.CreditCardSubscriptions[index];
                        add(Booked(account, -amount, description, category, account.Institution, OnDay(month, 1, rng.Next(0, 6))));
                    }
                    add(Booked(account, rng.Next(2000, 10000), "Credit card payment", TransactionCategory.Transfer,
                        account.Institution, OnDay(month, 28, 7)));
                    break;

                case SeedAccountType.Investment:
                    add(Booked(account, rng.Next(2000, 10000), "Monthly investment contribution", TransactionCategory.Transfer,
                        account.Institution, OnDay(month, 1, 9)));
                    if (month.Month is 3 or 6 or 9 or 12)
                        add(Booked(account, rng.Next(1000, 8000), "Portfolio quarterly return", TransactionCategory.Income,
                            account.Institution, OnDay(month, daysInMonth, 12)));
                    break;
            }

            for (var i = 0; i < account.VariablePerMonth; i++)
                add(Variable(account, RandomDay(month, 1, daysInMonth)));
        }

        private Transaction Variable(SeedAccount account, DateTime date)
        {
            var p = account.Profile;
            return account.Type switch
            {
                SeedAccountType.Savings => FromTemplate(account, SeedCatalog.SavingsTemplates, account.Institution, date),
                SeedAccountType.Investment => FromTemplate(account, SeedCatalog.InvestmentTemplates, account.Institution, date),
                SeedAccountType.CreditCard => CreditCardPurchase(account, date),
                _ => FromTemplate(account, SeedCatalog.CheckingTemplates, account.Institution, date)
            };
        }

        private Transaction FromTemplate(
            SeedAccount account,
            (string Description, TransactionCategory Category, int Min, int Max, bool IsExpense)[] templates,
            string institution,
            DateTime date)
        {
            var (description, category, min, max, isExpense) = templates[rng.Next(templates.Length)];
            var amount = rng.Next(min, max + 1);
            return Booked(account, isExpense ? -amount : amount, description, category, institution, date);
        }

        private Transaction CreditCardPurchase(SeedAccount account, DateTime date)
        {
            var (description, category, min, max) = SeedCatalog.CreditCardTemplates[rng.Next(SeedCatalog.CreditCardTemplates.Length)];
            return Booked(account, -rng.Next(min, max + 1), description, category, account.Institution, date);
        }

        private Transaction Booked(SeedAccount account, decimal amount, string description, TransactionCategory category, string institution, DateTime date) =>
            Transaction.Record(account.ExternalAccountId,
                Money.Create(amount, SupportedCurrency.Default),
                description,
                category,
                TransactionSource.Create(institution, $"seed-{runId:N}-{++_sequence:D6}"),
                date);

        private string Pick(string[] values) => values[rng.Next(values.Length)];

        private DateTime OnDay(DateTime month, int day, int hour) =>
            new(month.Year, month.Month, Math.Clamp(day, 1, DateTime.DaysInMonth(month.Year, month.Month)), hour, rng.Next(0, 60), 0, DateTimeKind.Utc);

        private DateTime RandomDay(DateTime month, int dayMin, int dayMax)
        {
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var day = rng.Next(Math.Max(1, dayMin), Math.Min(daysInMonth, dayMax) + 1);
            return new DateTime(month.Year, month.Month, day, rng.Next(7, 22), rng.Next(0, 60), 0, DateTimeKind.Utc);
        }

        private static DateTime StartOfMonth(DateTime value) => new(value.Year, value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    }
}