namespace BuildingBlocks.Application.Abstractions.Authentication
{
    // Admins read every institution; staff only their assigned ones. Part of the query cache key.
    public sealed record InstitutionAccess
    {
        private InstitutionAccess(bool allInstitutions, IReadOnlyList<string> institutions)
        {
            AllInstitutions = allInstitutions;
            Institutions = institutions;
        }

        public bool AllInstitutions { get; }

        public IReadOnlyList<string> Institutions { get; }

        public static InstitutionAccess All { get; } = new(true, []);

        public static InstitutionAccess None { get; } = new(false, []);

        public static InstitutionAccess Only(IEnumerable<string> institutions) =>
            new(false, institutions
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList());
    }
}