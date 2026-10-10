namespace Modules.Transactions.Application.Common.Inbox
{
    public static class InboundBank
    {
        public static bool Matches(string sourceName, string? institution) =>
            string.IsNullOrWhiteSpace(institution)
            || string.Equals(institution.Trim(), sourceName, StringComparison.OrdinalIgnoreCase);
    }
}