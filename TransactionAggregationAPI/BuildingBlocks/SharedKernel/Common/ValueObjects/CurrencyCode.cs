namespace SharedKernel.Common.ValueObjects
{
    public static class CurrencyCode
    {
        public const int Length = 3;

        public static bool IsWellFormed(string? currency) =>
            currency is not null && IsCanonical(Normalize(currency));

        public static bool IsCanonical(string? currency) =>
            currency is { Length: Length } && currency.All(c => c is >= 'A' and <= 'Z');

        public static string Normalize(string currency) => currency.Trim().ToUpperInvariant();
    }
}