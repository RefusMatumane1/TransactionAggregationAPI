namespace Modules.Transactions.Application.Common.Inbox
{
    // A source is one bank: the bank a delivery belongs to is the source it authenticated as. A
    // delivery may still name its institution (payload v2); if it does, it must be that same bank.
    public static class InboundBank
    {
        public static bool Matches(string sourceName, string? institution) =>
            string.IsNullOrWhiteSpace(institution)
            || string.Equals(institution.Trim(), sourceName, StringComparison.OrdinalIgnoreCase);
    }
}