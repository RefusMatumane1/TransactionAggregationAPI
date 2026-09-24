namespace Modules.Transactions.Application.Common.Options
{
    /// <summary>
    /// How each bank's raw transactions are turned into the canonical form (see
    /// TransactionNormalizer). Loaded from normalization-rules.json. <see cref="Default"/>
    /// applies to every institution, including ones with no profile of their own ("Other");
    /// an institution's profile only adds to it or overrides it.
    /// </summary>
    public sealed class NormalizationOptions
    {
        public const string SectionName = "Normalization";

        public InstitutionNormalizationProfile Default { get; set; } = new() { TimeZone = "UTC" };

        /// <summary>Keyed by institution name as BankLinks reports it (e.g. "FNB", "Absa"); case-insensitive.</summary>
        public Dictionary<string, InstitutionNormalizationProfile> Institutions { get; set; } = new();
    }

    public sealed class InstitutionNormalizationProfile
    {
        /// <summary>
        /// IANA time zone the bank's timestamps are in when they carry no offset. Null means
        /// "same as the default profile".
        /// </summary>
        public string? TimeZone { get; set; }

        /// <summary>
        /// Leading statement boilerplate stripped from descriptions (e.g. "POS PURCHASE"),
        /// matched case-insensitively and only as whole words. Added to the default profile's list.
        /// </summary>
        public List<string> DescriptionPrefixes { get; set; } = new();

        /// <summary>
        /// The bank's category labels mapped to ours (value: a TransactionCategory name).
        /// Added to the default profile's map; an entry here wins over the default's.
        /// </summary>
        public Dictionary<string, string> CategoryMap { get; set; } = new();
    }
}