namespace Modules.Transactions.Application.Common.Options
{
    public sealed class CategorizationOptions
    {
        public const string SectionName = "CategorizationRules";

        public Dictionary<string, string> Keywords { get; set; } = new();
    }
}