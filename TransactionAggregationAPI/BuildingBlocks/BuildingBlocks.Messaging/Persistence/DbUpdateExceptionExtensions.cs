using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BuildingBlocks.Messaging.Persistence
{
    public static class DbUpdateExceptionExtensions
    {
        public static bool IsUniqueViolation(this DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}