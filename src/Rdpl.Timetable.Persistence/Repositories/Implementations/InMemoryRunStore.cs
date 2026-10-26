using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>
/// Process-wide store behind the in-memory repositories (used when Persistence:Enabled is false). Singleton,
/// thread-safe (one lock), keeps every running run plus the newest <c>maxRetainedRuns</c> finished ones.
/// Nothing survives a restart — that is the original API behaviour.
/// </summary>
public sealed class InMemoryRunStore
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, StoredRun> _runs = new();
    private readonly int _maxRetainedRuns;

    public InMemoryRunStore(int maxRetainedRuns)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetainedRuns);
        _maxRetainedRuns = maxRetainedRuns;
    }

    public void Add(RunRecord record)
    {
        lock (_lock)
        {
            if (!_runs.TryAdd(record.Id, new StoredRun(record, null)))
            {
                throw new InvalidOperationException($"Run {record.Id} already exists.");
            }

            Trim();
        }
    }

    /// <summary>Atomically replaces a stored run; <paramref name="update"/> may throw to reject the change.</summary>
    public void Update(Guid runId, Func<StoredRun, StoredRun> update)
    {
        lock (_lock)
        {
            if (!_runs.TryGetValue(runId, out var current))
            {
                throw new InvalidOperationException($"Run {runId} does not exist.");
            }

            _runs[runId] = update(current);
            Trim();
        }
    }

    public StoredRun? Get(Guid runId)
    {
        lock (_lock)
        {
            return _runs.TryGetValue(runId, out var run) ? run : null;
        }
    }

    public IReadOnlyList<StoredRun> Snapshot()
    {
        lock (_lock)
        {
            return _runs.Values.ToList();
        }
    }

    private void Trim()
    {
        // Never evict the newest COMPLETED run of each kind (generation / data validation): it is what
        // /api/section, /api/teacher, /api/conflict and /result serve. Same ordering as GetLatestCompletedRunAsync.
        var latestPerKind = _runs.Values.Select(r => r.Record)
            .Where(r => r.State == RunState.Completed)
            .GroupBy(r => r.Kind)
            .Select(g => g.OrderByDescending(r => r.CompletedUtc).ThenByDescending(r => r.CreatedUtc).First().Id)
            .ToHashSet();

        var old = _runs.Values.Select(r => r.Record)
            .Where(r => r.State != RunState.Running)
            .OrderByDescending(r => r.CreatedUtc)
            .Skip(_maxRetainedRuns)
            .Where(r => !latestPerKind.Contains(r.Id))
            .Select(r => r.Id)
            .ToList();
        foreach (var id in old)
        {
            _runs.Remove(id);
        }
    }
}

/// <summary>A run record plus everything it produced (null until completed).</summary>
public sealed record StoredRun(RunRecord Record, RunResult? Result);
