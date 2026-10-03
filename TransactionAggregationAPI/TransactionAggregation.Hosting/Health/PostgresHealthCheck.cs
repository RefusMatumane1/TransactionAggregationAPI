using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TransactionAggregation.Hosting.Health
{
    internal sealed class PostgresHealthCheck(NpgsqlConnection connection) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "PostgreSQL is unreachable", ex);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}