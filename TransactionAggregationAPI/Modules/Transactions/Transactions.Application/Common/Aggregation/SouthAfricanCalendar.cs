namespace Modules.Transactions.Application.Common.Aggregation
{
    public static class SouthAfricanCalendar
    {
        public const double UtcOffsetHours = 2;

        private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(UtcOffsetHours);

        public static DateOnly Today(TimeProvider time) =>
            DayOf(time.GetUtcNow().UtcDateTime);

        public static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(utc + UtcOffset);

        public static DateOnly StartOfWeek(DateOnly date) =>
            date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

        public static DateOnly StartOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
    }
}