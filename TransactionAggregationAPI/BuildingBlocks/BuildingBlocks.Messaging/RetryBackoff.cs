namespace BuildingBlocks.Messaging
{
    public static class RetryBackoff
    {
        public const double MaxSeconds = 300;

        public static TimeSpan After(int attempts) => After(attempts, Random.Shared);

        public static TimeSpan After(int attempts, Random random)
        {
            var ceiling = Ceiling(attempts).TotalSeconds;
            return TimeSpan.FromSeconds(ceiling / 2 + random.NextDouble() * ceiling / 2);
        }

        public static TimeSpan Ceiling(int attempts) =>
            TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(attempts, 30) + 1), MaxSeconds));
    }
}