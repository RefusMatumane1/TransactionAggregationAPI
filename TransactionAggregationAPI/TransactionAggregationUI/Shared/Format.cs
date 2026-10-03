using System.Globalization;

namespace TransactionAggregationUI.Shared
{
    // One way to show money, dates and changes everywhere in the app. Booking dates are shown on the
    // South African calendar (UTC+2, no DST), the same days the server aggregates by.
    public static class Format
    {
        private static readonly CultureInfo Zar = CreateZar();
        private static readonly TimeSpan SouthAfricaOffset = TimeSpan.FromHours(2);

        private static CultureInfo CreateZar()
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-ZA").Clone();
            // en-ZA groups with a non-breaking space and uses a decimal comma; a decimal point and
            // comma groups read unambiguously next to the ISO amounts banks send.
            culture.NumberFormat.CurrencyDecimalSeparator = ".";
            culture.NumberFormat.CurrencyGroupSeparator = ",";
            culture.NumberFormat.NumberDecimalSeparator = ".";
            culture.NumberFormat.NumberGroupSeparator = ",";
            culture.NumberFormat.CurrencySymbol = "R";
            culture.NumberFormat.CurrencyPositivePattern = 0; // R1.00
            culture.NumberFormat.CurrencyNegativePattern = 1; // -R1.00
            return culture;
        }

        public static string Money(decimal amount, string currency = "ZAR") =>
            currency == "ZAR"
                ? amount.ToString("C2", Zar)
                : $"{(amount < 0 ? "-" : "")}{currency} {Math.Abs(amount).ToString("N2", Zar)}";

        // An amount whose direction matters (a transaction): always signed, credits with "+".
        public static string SignedMoney(decimal amount, string currency = "ZAR") =>
            amount > 0 ? "+" + Money(amount, currency) : Money(amount, currency);

        // Axis ticks and tight spaces: R12.5k, R1.2m.
        public static string CompactMoney(decimal amount)
        {
            var abs = Math.Abs(amount);
            var sign = amount < 0 ? "-" : "";
            return abs switch
            {
                >= 1_000_000 => $"{sign}R{(abs / 1_000_000).ToString("0.#", Zar)}m",
                >= 1_000 => $"{sign}R{(abs / 1_000).ToString("0.#", Zar)}k",
                _ => $"{sign}R{abs.ToString("0", Zar)}"
            };
        }

        public static string Count(int value) => value.ToString("N0", Zar);

        public static string Percent(decimal share) => (share * 100).ToString("0.#", Zar) + "%";

        public static string Change(decimal percent) => percent.ToString("+0.#;-0.#;0", Zar) + "%";

        public static DateTime ToSouthAfrica(DateTime utc) =>
            DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + SouthAfricaOffset, DateTimeKind.Unspecified);

        public static string BookingDate(DateTime utc) => ToSouthAfrica(utc).ToString("d MMM yyyy", Zar);

        public static string Timestamp(DateTime utc) => ToSouthAfrica(utc).ToString("d MMM yyyy, HH:mm", Zar);

        public static string Day(DateOnly day) => day.ToString("d MMM yyyy", Zar);

        public static string Month(DateOnly day) => day.ToString("MMM yyyy", Zar);

        public static string ShortMonth(DateOnly day) => day.ToString("MMM", Zar);

        public static string Invariant(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateOnly? ParseDay(string? value) =>
            DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

        public static DateOnly TodayInSouthAfrica() => DateOnly.FromDateTime(DateTime.UtcNow + SouthAfricaOffset);
    }
}