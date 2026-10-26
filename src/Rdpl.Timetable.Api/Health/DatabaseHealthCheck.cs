using Microsoft.Extensions.Diagnostics.HealthChecks;
using Rdpl.Timetable.Persistence;

namespace Rdpl.Timetable.Api.Health;

/// <summary>
/// Readiness probe for PostgreSQL. Reports only healthy/unhealthy; the exception is logged server-side and never
/// returned to the caller (it can contain host names).
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext db, ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _db.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Healthy();
            }

            // CanConnectAsync swallows connection/authentication errors and returns false, so log here.
            _logger.LogWarning("Database health check failed: cannot connect (check host, credentials and firewall).");
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Database health check failed.");
            return HealthCheckResult.Unhealthy("Database unreachable.");
        }
    }
}
