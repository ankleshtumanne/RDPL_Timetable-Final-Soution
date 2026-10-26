using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>In-memory <see cref="ITimetableRepository"/> (Persistence:Enabled = false).</summary>
public sealed class InMemoryTimetableRepository : ITimetableRepository
{
    private readonly InMemoryRunStore _store;

    public InMemoryTimetableRepository(InMemoryRunStore store)
    {
        _store = store;
    }

    public bool IsDurable => false;

    public Task AddRunAsync(NewRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        _store.Add(new RunRecord
        {
            Id = run.Id,
            Kind = run.Kind,
            State = RunState.Running,
            Profile = run.Profile,
            CreatedUtc = run.CreatedUtc,
            DataSourceKind = "PENDING",
            TimeLimitSeconds = run.TimeLimitSeconds,
        });
        return Task.CompletedTask;
    }

    public Task CompleteRunAsync(RunResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        _store.Update(result.RunId, current =>
        {
            if (current.Record.State != RunState.Running)
            {
                throw new InvalidOperationException($"Run {result.RunId} is {current.Record.State}; only running runs can be completed.");
            }

            var record = current.Record with
            {
                State = result.State,
                CompletedUtc = result.CompletedUtc,
                Outcome = result.Outcome,
                Success = result.Success,
                DataSourceKind = result.DataSourceKind,
                UsesDummyData = result.UsesDummyData,
                SolverStatus = result.SolverStatus,
                WallTimeMs = result.WallTimeMs,
                HardConstraintsSatisfied = result.HardConstraintsSatisfied,
                SectionsScheduled = result.SectionsScheduled,
                TotalSlotsScheduled = result.TotalSlotsScheduled,
                SolverJson = result.SolverJson,
                SummaryJson = result.SummaryJson,
                DiagnosticJson = result.DiagnosticJson,
                LessonCount = result.Lessons.Count,
                DataConflictCount = result.DataConflicts.Count,
                SchedulingConflictCount = result.SchedulingConflicts.Count,
            };
            return new StoredRun(record, result);
        });
        return Task.CompletedTask;
    }

    public Task<bool> FailRunAsync(Guid runId, string error, DateTimeOffset completedUtc, CancellationToken cancellationToken)
    {
        var failed = false;
        try
        {
            _store.Update(runId, current =>
            {
                if (current.Record.State != RunState.Running)
                {
                    return current;
                }

                failed = true;
                return current with { Record = current.Record with { State = RunState.Failed, Error = error, CompletedUtc = completedUtc } };
            });
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(false); // unknown run (e.g. trimmed)
        }

        return Task.FromResult(failed);
    }

    public Task<RunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult(_store.Get(runId)?.Record);

    public Task<RunRecord?> GetLatestCompletedRunAsync(RunKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Snapshot().Select(e => e.Record)
            .Where(r => r.Kind == kind && r.State == RunState.Completed)
            .OrderByDescending(r => r.CompletedUtc).ThenByDescending(r => r.CreatedUtc)
            .FirstOrDefault());

    public Task<string?> GetReportJsonAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = _store.Get(runId);
        return Task.FromResult(run is { Record.State: RunState.Completed } ? run.Result?.ReportJson : null);
    }

    public Task<IReadOnlyList<RunRecord>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        IReadOnlyList<RunRecord> page = _store.Snapshot().Select(e => e.Record).OrderByDescending(r => r.CreatedUtc).ThenBy(r => r.Id).Skip(skip).Take(take).ToList();
        return Task.FromResult(page);
    }

    public Task<int> CountRunsAsync(CancellationToken cancellationToken) => Task.FromResult(_store.Snapshot().Count);

    public Task<int> FailAbandonedRunsAsync(string error, CancellationToken cancellationToken) => Task.FromResult(0);
}
