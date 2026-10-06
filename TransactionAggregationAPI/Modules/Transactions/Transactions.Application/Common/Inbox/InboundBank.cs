namespace Modules.Transactions.Application.Common.Inbox
{
    // The bank is the source the delivery authenticated as; a delivery that names its institution (v2) must name that bank.
    public static class InboundBank
    {
        public static bool Matches(string sourceName, string? institution) =>
            string.IsNullOrWhiteSpace(institution)
            || string.Equals(institution.Trim(), sourceName, StringComparison.OrdinalIgnoreCase);
    }
}