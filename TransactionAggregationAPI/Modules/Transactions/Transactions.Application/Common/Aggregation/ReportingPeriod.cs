namespace Modules.Transactions.Application.Common.Aggregation
{
    public sealed record ReportingPeriod(DateOnly From, DateOnly To)
    {
        public const int MaxDays = 3_660;

        public DateTime FromUtc => SouthAfricanCalendar.StartOfDayUtc(From);

        public DateTime ToUtcExclusive => SouthAfricanCalendar.StartOfDayUtc(To.AddDays(1));

        public int Days => To.DayNumber - From.DayNumber + 1;

        public bool IsValid => From <= To && Days <= MaxDays;

        public ReportingPeriod Previous() => new(From.AddDays(-Days), From.AddDays(-1));

        public static ReportingPeriod Resolve(DateOnly? from, DateOnly? to, TimeProvider time)
        {
            var end = to ?? SouthAfricanCalendar.Today(time);
            var start = from ?? end.AddMonths(-12).AddDays(1);
            return new ReportingPeriod(start, end);
        }
    }
}