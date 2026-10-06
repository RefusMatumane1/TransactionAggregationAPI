namespace BuildingBlocks.Web
{
    public static class RateLimitPolicies
    {
        public const string FixedWindow = "FixedWindow";
    }

    public static class AuthorizationPolicies
    {
        public const string Admin = "Admin";

        public const string Staff = "Staff";
    }

    // Keycloak realm roles; there are no customer accounts.
    public static class Roles
    {
        public const string Admin = "admin";
        public const string Staff = "staff";
    }

    public static class ClaimNames
    {
        // Institution codes a staff member may read (Keycloak user attribute "institutions").
        public const string Institutions = "institutions";
    }
}