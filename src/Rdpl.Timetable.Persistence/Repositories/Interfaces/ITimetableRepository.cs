using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Interfaces;

/// <summary>
/// Stores timetable runs and everything they produced. Two implementations:
/// <c>TimetableRepository</c> (EF Core + PostgreSQL, when Persistence:Enabled) and
/// <c>InMemoryTimetableRepository</c> (default; keeps the original in-memory behaviour, nothing survives a restart).
/// All methods are asynchronous and accept a cancellation token.
/// </summary>
public interface ITimetableRepository
{
    /// <summary>True when data survives an application restart (PostgreSQL).</summary>
    bool IsDurable { get; }

    /// <summary>Inserts a run in state <see cref="RunState.Running"/>.</summary>
    Task AddRunAsync(NewRun run, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks the run completed and stores master data (upsert), input assignment rows, lessons and conflicts.
    /// Throws <see cref="InvalidOperationException"/> if the run does not exist or is no longer running.
    /// </summary>
    Task CompleteRunAsync(RunResult result, CancellationToken cancellationToken);

    /// <summary>Marks a running run as failed with a user-safe message. Returns false if it was not running.</summary>
    Task<bool> FailRunAsync(Guid runId, string error, DateTimeOffset completedUtc, CancellationToken cancellationToken);

    Task<RunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Most recently completed run of a kind (any outcome, e.g. INFEASIBLE included), or null.</summary>
    Task<RunRecord?> GetLatestCompletedRunAsync(RunKind kind, CancellationToken cancellationToken);

    /// <summary>The stored JSON report text, or null when the run has none (running/failed/unknown).</summary>
    Task<string?> GetReportJsonAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<RunRecord>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountRunsAsync(CancellationToken cancellationToken);

    /// <summary>Marks runs left in <see cref="RunState.Running"/> (e.g. by a crash) as failed. Returns how many.</summary>
    Task<int> FailAbandonedRunsAsync(string error, CancellationToken cancellationToken);
}
