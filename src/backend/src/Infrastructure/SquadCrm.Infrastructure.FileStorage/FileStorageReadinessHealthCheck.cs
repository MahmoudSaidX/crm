using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SquadCrm.Infrastructure.FileStorage;

internal sealed class FileStorageReadinessHealthCheck(FileStorageOptions options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string probePath = Path.Combine(options.LocalRootPath, $".health-{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(options.LocalRootPath);
            await File.WriteAllBytesAsync(probePath, [], cancellationToken);
            File.Delete(probePath);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            try
            {
                File.Delete(probePath);
            }
            catch (Exception cleanupException) when (cleanupException is not OperationCanceledException)
            {
                // Keep readiness diagnostics generic even when cleanup is also unavailable.
            }

            return HealthCheckResult.Unhealthy("File storage is unavailable.");
        }
    }
}
