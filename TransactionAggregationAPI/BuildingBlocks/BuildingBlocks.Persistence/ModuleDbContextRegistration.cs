using Microsoft.EntityFrameworkCore;
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
    }
}