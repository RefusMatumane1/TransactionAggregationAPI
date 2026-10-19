namespace BuildingBlocks.Persistence.Migrations
{
    /// <summary>One module's database migration step. Modules register theirs; the host runs them in registration order.</summary>
    public interface IModuleMigration
    {
        string Name { get; }

        Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken);
    }
}
