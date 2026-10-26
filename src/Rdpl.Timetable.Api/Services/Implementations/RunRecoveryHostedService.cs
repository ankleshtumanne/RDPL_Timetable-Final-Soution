using Rdpl.Timetable.Persistence.Repositories.Interfaces;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// On startup (PostgreSQL only): runs still marked RUNNING belong to a process that no longer exists, so they are
/// marked FAILED. Assumes a single API instance runs generations (the brief's deployment model).
/// A database outage at startup is logged and does not stop the API.
/// </summary>
public sealed class RunRecoveryHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RunRecoveryHostedService> _logger;

    public RunRecoveryHostedService(IServiceScopeFactory scopes, ILogger<RunRecoveryHostedService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITimetableRepository>();
            var count = await repository.FailAbandonedRunsAsync("The server restarted before this run finished.", cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                _logger.LogWarning("Marked {Count} abandoned run(s) as failed.", count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not check for abandoned runs at startup (database unavailable?).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
