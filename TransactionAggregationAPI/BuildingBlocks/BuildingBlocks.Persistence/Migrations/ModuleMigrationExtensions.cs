using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Persistence.Migrations
{
    public static class ModuleMigrationExtensions
    {
        /// <summary>Migrates every registered module database, in registration order.</summary>
        public static async Task ApplyModuleMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            foreach (var migration in services.GetServices<IModuleMigration>())
                await migration.MigrateAsync(services, cancellationToken);
        }
    }
}
