using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Repositories.Models;

/// <summary>Read model of a run without the (large) report text.</summary>
public sealed record RunRecord
{
    public required Guid Id { get; init; }

    public required RunKind Kind { get; init; }

    public required RunState State { get; init; }

    public required string Profile { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? CompletedUtc { get; init; }

    public string? Outcome { get; init; }

    public bool? Success { get; init; }

    public required string DataSourceKind { get; init; }

    public bool UsesDummyData { get; init; }

    public string? SolverStatus { get; init; }

    public long? WallTimeMs { get; init; }

    public int TimeLimitSeconds { get; init; }

    public bool? HardConstraintsSatisfied { get; init; }

    public int? SectionsScheduled { get; init; }

    public int? TotalSlotsScheduled { get; init; }

    public string? Error { get; init; }

    public string? SolverJson { get; init; }

    public string? SummaryJson { get; init; }

    public string? DiagnosticJson { get; init; }

    public int LessonCount { get; init; }

    public int DataConflictCount { get; init; }

    public int SchedulingConflictCount { get; init; }
}

/// <summary>A new run as created when a request is accepted.</summary>
public sealed record NewRun(Guid Id, RunKind Kind, string Profile, DateTimeOffset CreatedUtc, int TimeLimitSeconds, int Workers, int RandomSeed);
