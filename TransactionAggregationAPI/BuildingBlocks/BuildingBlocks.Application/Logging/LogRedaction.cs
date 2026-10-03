namespace BuildingBlocks.Application.Logging
{
    // Identifiers that can point at a person (a bank account) are logged masked: enough to correlate
    // with the audit trail, which holds the full value under access control, but not the value itself.
    public static class LogRedaction
    {
        private const int VisibleSuffixLength = 4;

        public static string Account(string? externalAccountId)
        {
            if (string.IsNullOrEmpty(externalAccountId))
                return "(none)";

            return externalAccountId.Length <= VisibleSuffixLength
                ? new string('*', externalAccountId.Length)
                : "***" + externalAccountId[^VisibleSuffixLength..];
        }
    }
}