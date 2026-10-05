namespace BuildingBlocks.Web
{
    public static class RateLimitPolicies
    {
        public const string FixedWindow = "FixedWindow";
    }

    public static class AuthorizationPolicies
    {
        public const string Admin = "Admin";

        // Read access to transactions and aggregates: the staff or admin realm role.
        public const string Staff = "Staff";
    }

    // Keycloak realm roles. Every signed-in user holds one of these; there are no customer accounts.
    public static class Roles
    {
        public const string Admin = "admin";
        public const string Staff = "staff";
    }

    public static class ClaimNames
    {
        // Multi-valued: the institution (bank) codes a staff member may read. Mapped from the
        // Keycloak user attribute "institutions".
        public const string Institutions = "institutions";
    }
}