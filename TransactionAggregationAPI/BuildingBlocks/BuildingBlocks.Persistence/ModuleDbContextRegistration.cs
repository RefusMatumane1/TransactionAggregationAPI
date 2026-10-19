using BuildingBlocks.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BuildingBlocks.Persistence
{
    public static class ModuleDbContextRegistration
    {
        public const string ConnectionStringName = "transactiondb";

        public static NpgsqlDbContextOptionsBuilder UseModuleDefaults<TContext>(this NpgsqlDbContextOptionsBuilder npgsql)
            where TContext : DbContext =>
            npgsql
                .MigrationsAssembly(typeof(TContext).Assembly.GetName().Name)
                .EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);

        /// <summary>
        /// Registers a module's DbContext on the shared connection, exposes it through the abstraction the module's
        /// Application layer depends on, and enrols it in <see cref="IModuleMigration"/>. Migrations run in the order
        /// modules are registered, so a module that builds on another's tables registers after it.
        /// </summary>
        public static IServiceCollection AddModuleDbContext<TContext, TService>(
            this IServiceCollection services, Action<DbContextOptionsBuilder>? configure = null)
            where TContext : DbContext, TService
            where TService : class
        {
            services.AddDbContext<TContext>((sp, options) =>
            {
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(), npgsql => npgsql.UseModuleDefaults<TContext>());
                configure?.Invoke(options);
            });
            services.AddScoped<TService>(sp => sp.GetRequiredService<TContext>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleMigration, ModuleMigration<TContext>>());

            return services;
        }
    }
}
