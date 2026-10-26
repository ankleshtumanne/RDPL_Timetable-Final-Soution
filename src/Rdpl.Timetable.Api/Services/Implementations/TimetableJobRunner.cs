using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Data;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// Background executor (replaces the former TimetableJobService). A full-school solve takes minutes, longer than an
/// HTTP request should stay open, so it runs on the thread pool. Only ONE solve at a time. The finished run is mapped
/// with <see cref="RunResultMapper"/> and stored through a scoped <see cref="ITimetableRepository"/>.
/// </summary>
public sealed class TimetableJobRunner : ITimetableJobRunner, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDatasetProvider _datasets;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TimetableJobRunner> _logger;
    private readonly CancellationToken _stopping;

    public TimetableJobRunner(IDatasetProvider datasets, IServiceScopeFactory scopes, ILogger<TimetableJobRunner> logger, IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        _datasets = datasets;
        _scopes = scopes;
        _logger = logger;
        _stopping = lifetime.ApplicationStopping;
    }

    public bool IsBusy => _gate.CurrentCount == 0;

    /// <summary>Latest finished generation of this process (Core objects; diagnostics and tests only — endpoints read the repository).</summary>
    public bool TryReserve() => _gate.Wait(0);

    public void CancelReservation() => _gate.Release();

    public void Start(Guid runId, RunSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = Task.Run(() => ExecuteAsync(runId, settings), CancellationToken.None);
    }

    private async Task ExecuteAsync(Guid runId, RunSettings settings)
    {
        try
        {
            _logger.LogInformation("Run {RunId}: generating with profile {Profile}, limit {Limit}s.", runId, settings.Rules.Profile, settings.Solver.TimeLimitSeconds);
            LoadedDataset dataset;
            TimetableRun run;
            try
            {
                dataset = _datasets.Load();
                run = TimetableEngine.Run(dataset, settings.Rules, settings.Solver, solve: true, _stopping);
            }
            catch (DataInputException ex)
            {
                _logger.LogWarning("Run {RunId}: input error: {Message}", runId, ex.Message);
                await FailAsync(runId, ex.Message).ConfigureAwait(false); // DataInputException messages are user-safe.
                return;
            }
            catch (OperationCanceledException)
            {
                await FailAsync(runId, "The server is shutting down; the job was cancelled.").ConfigureAwait(false);
                return;
            }

            var result = RunResultMapper.Map(runId, dataset, run, settings.Rules, DateTimeOffset.UtcNow);
            await using (var scope = _scopes.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<ITimetableRepository>();
                await repository.CompleteRunAsync(result, CancellationToken.None).ConfigureAwait(false);
            }

            _logger.LogInformation("Run {RunId}: {Outcome} (solver {Status}); stored {Lessons} lessons, {DataConflicts} data conflicts.",
                runId, run.Report.Outcome, run.Report.Solver.Status, result.Lessons.Count, result.DataConflicts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId}: unexpected failure.", runId);
            await FailAsync(runId, "Unexpected server error while generating or storing the timetable. See server logs for run " + runId.ToString("N") + ".").ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task FailAsync(Guid runId, string message)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITimetableRepository>();
            await repository.FailRunAsync(runId, message, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The run stays RUNNING; RunRecoveryHostedService marks it failed on the next start.
            _logger.LogError(ex, "Run {RunId}: could not record the failure.", runId);
        }
    }

    public void Dispose() => _gate.Dispose();
}
