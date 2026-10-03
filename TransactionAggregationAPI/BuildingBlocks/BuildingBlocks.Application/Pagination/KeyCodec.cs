using System.Globalization;

namespace BuildingBlocks.Application.Pagination
{
    public sealed class KeyCodec<TKey>(Func<TKey, string> format, KeyCodec<TKey>.Parser parse)
    {
        public delegate bool Parser(string value, out TKey key);

        public string Format(TKey key) => format(key);

        public bool TryParse(string value, out TKey key) => parse(value, out key);
    }

    public static class KeyCodecs
    {
        public static readonly KeyCodec<DateTime> UtcDateTime = new(
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            (string value, out DateTime key) =>
            {
                var parsed = DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out key);
                key = DateTime.SpecifyKind(key, DateTimeKind.Utc);
                return parsed;
            });

        public static readonly KeyCodec<decimal> Decimal = new(
            value => value.ToString(CultureInfo.InvariantCulture),
            (string value, out decimal key) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out key));

        public static readonly KeyCodec<string> String = new(
            value => value,
            (string value, out string key) =>
            {
                key = value;
                return true;
            });

        public static KeyCodec<TEnum> Enum<TEnum>() where TEnum : struct, System.Enum => new(
            value => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            (string value, out TEnum key) =>
            {
                key = default;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                    || !System.Enum.IsDefined(typeof(TEnum), number))
                    return false;
                key = (TEnum)System.Enum.ToObject(typeof(TEnum), number);
                return true;
            });
    }
}