namespace Modules.Transactions.Application.Common.Options
{
    public sealed class NormalizationOptions
    {
        public const string SectionName = "Normalization";

        public InstitutionNormalizationProfile Default { get; set; } = new() { TimeZone = "UTC" };

        public Dictionary<string, InstitutionNormalizationProfile> Institutions { get; set; } = new();
    }

    public sealed class InstitutionNormalizationProfile
    {
        public string? TimeZone { get; set; }

        public List<string> DescriptionPrefixes { get; set; } = new();

        public Dictionary<string, string> CategoryMap { get; set; } = new();
    }
}