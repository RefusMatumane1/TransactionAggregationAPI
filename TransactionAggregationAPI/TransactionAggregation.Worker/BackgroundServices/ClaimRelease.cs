namespace TransactionAggregation.Worker.BackgroundServices
{
    // On shutdown, claims that were never worked on are handed back so another replica picks them up
    // straight away instead of after the claim timeout. Best effort: if the database is unreachable
    // the claims simply expire and are reclaimed later.
    internal static class ClaimRelease
    {
        private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

        public static async Task ReleaseAsync<TMessage>(
            IEnumerable<TMessage> unprocessed,
            Action<TMessage> release,
            Func<CancellationToken, Task<int>> save,
            ILogger logger,
            string queue)
        {
            var messages = unprocessed.ToList();
            if (messages.Count == 0)
                return;

            foreach (var message in messages)
                release(message);

            using var timeout = new CancellationTokenSource(Budget);
            try
            {
                await save(timeout.Token);
                logger.LogInformation("Shutting down: released {Count} unprocessed {Queue} claims", messages.Count, queue);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Shutting down: could not release {Count} {Queue} claims; they will be reclaimed when they expire",
                    messages.Count, queue);
            }
        }
    }
}