using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BuildingBlocks.Persistence
{
    // Module contexts share the request's NpgsqlConnection so their writes join one transaction.
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