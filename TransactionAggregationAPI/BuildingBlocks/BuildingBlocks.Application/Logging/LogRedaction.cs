namespace BuildingBlocks.Application.Logging
{
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