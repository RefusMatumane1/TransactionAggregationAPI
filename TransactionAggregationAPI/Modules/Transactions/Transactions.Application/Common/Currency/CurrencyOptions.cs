using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Common.Currency
{
    public sealed class CurrencyOptions
    {
        public const string SectionName = "Currencies";

        public const decimal MaxRate = 1_000_000_000m;

        public string BaseCurrency { get; set; } = string.Empty;

        public Dictionary<string, decimal> Rates { get; set; } = new(StringComparer.Ordinal);

        public static bool IsValid(CurrencyOptions options) => Validate(options) is null;

        public static string? Validate(CurrencyOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.BaseCurrency))
                return "Currencies:BaseCurrency must be set.";
            if (!CurrencyCode.IsCanonical(options.BaseCurrency))
                return $"Currencies:BaseCurrency '{options.BaseCurrency}' must be a 3-letter upper-case currency code.";

            foreach (var (code, rate) in options.Rates)
            {
                if (!CurrencyCode.IsCanonical(code))
                    return $"Currencies:Rates:{code} is not a 3-letter upper-case currency code.";
                if (rate < 0 || rate > MaxRate)
                    return $"Currencies:Rates:{code} must be between 0 (switched off) and {MaxRate}.";
                if (code == options.BaseCurrency && rate is not (0m or 1m))
                    return $"Currencies:Rates:{code} is the base currency, so its rate must be 1 (or omitted).";
            }

            return null;
        }
    }
}