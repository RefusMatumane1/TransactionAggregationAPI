namespace TransactionAggregation.Application.Common.Options
{
    /// <summary>
    /// Keyword-to-category mapping rules loaded from configuration.
    /// Keys are lowercase merchant/description keywords; values are category names matching TransactionCategory enum.
    /// </summary>
    public sealed class CategorizationOptions
    {
        public const string SectionName = "CategorizationRules";

        /// <summary>
        /// Dictionary of keyword (lowercase) → category name.
        /// Example: { "walmart": "Groceries", "uber": "Transportation" }
        /// </summary>
        public Dictionary<string, string> Keywords { get; set; } = new();
    }
}
