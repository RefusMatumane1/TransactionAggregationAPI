namespace Modules.Transactions.Infrastructure.BackgroundServices
{
    public class PendingExpiryOptions
    {
        public const string SectionName = "PendingExpiry";

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// How long a transaction may stay Pending (counted from Transaction.PendingSince) before
        /// it's assumed the bank dropped the authorisation. Card authorisations typically lapse
        /// within about 7 days; raise this if your banks routinely post later (e.g. hotel or car
        /// hire pre-authorisations). A posting that arrives after expiry still settles the row.
        /// </summary>
        public int ExpireAfterDays { get; set; } = 7;

        public int CheckIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// Rows expired per unit of work. A full batch means there may be more, so the job
        /// immediately runs another rather than waiting CheckIntervalMinutes.
        /// </summary>
        public int BatchSize { get; set; } = 200;
    }
}