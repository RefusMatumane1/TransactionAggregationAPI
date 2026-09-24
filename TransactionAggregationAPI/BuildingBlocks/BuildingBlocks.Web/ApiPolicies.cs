namespace BuildingBlocks.Web
{
    /// <summary>
    /// Policy names shared by the host, which registers them, and the modules' endpoints,
    /// which require them — one definition so the two can't drift apart.
    /// </summary>
    public static class RateLimitPolicies
    {
        public const string FixedWindow = "FixedWindow";
    }

    public static class AuthorizationPolicies
    {
        public const string Admin = "Admin";
    }
}