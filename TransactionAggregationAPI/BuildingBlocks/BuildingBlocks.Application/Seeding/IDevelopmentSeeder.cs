namespace BuildingBlocks.Application.Seeding
{
    /// <summary>Loads one module's demo data. Registered by the module itself, and only in Development.</summary>
    public interface IDevelopmentSeeder
    {
        Task SeedAsync(CancellationToken cancellationToken);
    }
}
