namespace BuildingBlocks.Application.Abstractions.Authentication
{
    // Which institutions' data a caller may read. Admins read every institution; staff read only the
    // institutions assigned to them in the identity provider, and nothing when none are assigned.
    // Carried on every scoped query, so it is part of the query's cache key.
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