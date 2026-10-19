using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Application.Seeding
{
    /// <summary>One registered seeder. Kept apart from the seeder itself so the runner can order them without building any.</summary>
    public sealed record DevelopmentSeederRegistration(Type SeederType);

    public static class DevelopmentSeeding
    {
        public static IServiceCollection AddDevelopmentSeeder<TSeeder>(this IServiceCollection services)
            where TSeeder : class, IDevelopmentSeeder
        {
            if (services.Any(d => d.ServiceType == typeof(DevelopmentSeederRegistration)
                                  && ((DevelopmentSeederRegistration)d.ImplementationInstance!).SeederType == typeof(TSeeder)))
                return services;

            services.TryAddScoped<TSeeder>();
            services.AddSingleton(new DevelopmentSeederRegistration(typeof(TSeeder)));
            return services;
        }

        /// <summary>Runs every registered seeder in registration order, each in a scope of its own.</summary>
        public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
        {
            foreach (var registration in services.GetServices<DevelopmentSeederRegistration>())
            {
                using var scope = services.CreateScope();
                var seeder = (IDevelopmentSeeder)scope.ServiceProvider.GetRequiredService(registration.SeederType);
                await seeder.SeedAsync(cancellationToken);
            }
        }
    }
}
