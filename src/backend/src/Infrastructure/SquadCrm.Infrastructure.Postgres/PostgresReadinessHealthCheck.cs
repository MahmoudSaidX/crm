using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SquadCrm.Infrastructure.Postgres;

internal sealed class PostgresReadinessHealthCheck(PostgresOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlConnection connection = new(options.BuildConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}
