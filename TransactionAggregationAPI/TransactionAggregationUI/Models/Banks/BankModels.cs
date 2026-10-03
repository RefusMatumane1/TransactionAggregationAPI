namespace TransactionAggregationUI.Models.Banks
{
    // A bank as staff see it (GET /api/v1/banks): the code transactions carry, and how to show it.
    public class BankModel
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Color { get; set; } = BankDirectory.UnknownColor;
        public bool IsActive { get; set; }
        public DateTime? LastDeliveryAt { get; set; }
    }

    // A bank as admins manage it (GET /api/v1/admin/webhook-sources).
    public class BankSourceModel
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Color { get; set; } = BankDirectory.UnknownColor;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool SigningKeyRegistered { get; set; }
    }

    public class CreateBankResultModel
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }

    public class RotateKeyResultModel
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public static class BankDirectory
    {
        public const string UnknownColor = "#6C757D";

        // A transaction can name a bank that no longer has (or never had) a source row, e.g. data
        // stored before sources were banks; it is still shown, under its code, in grey.
        public static BankModel Find(IReadOnlyDictionary<string, BankModel> banks, string code) =>
            banks.TryGetValue(code, out var bank)
                ? bank
                : new BankModel { Code = code, DisplayName = code, Color = UnknownColor };

        public static string DescribeAge(DateTime? utc)
        {
            if (utc is not { } at)
                return "never";

            var age = DateTime.UtcNow - at;
            return age.TotalMinutes switch
            {
                < 1 => "just now",
                < 60 => $"{(int)age.TotalMinutes} min ago",
                < 60 * 24 => $"{(int)age.TotalHours} h ago",
                _ => $"{(int)age.TotalDays} d ago"
            };
        }
    }
}