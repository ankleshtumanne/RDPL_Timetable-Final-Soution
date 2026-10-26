namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>
/// One data-validation or generation run. Everything a run produced (input assignment rows used, generated lessons,
/// data conflicts, scheduling conflicts) hangs off this row and is deleted with it (cascade).
/// </summary>
public class TimetableRunEntity
{
    /// <summary>Same value as the API job id.</summary>
    public Guid Id { get; set; }

    public RunKind Kind { get; set; }

    public RunState State { get; set; }

    /// <summary>"strict" or "pragmatic".</summary>
    public required string Profile { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>e.g. TIMETABLE_GENERATED, INFEASIBLE, DATA_VALIDATION_ONLY.</summary>
    public string? Outcome { get; set; }

    public bool? Success { get; set; }

    /// <summary>
    /// e.g. "DUMMY_ASSIGNMENTS" or "CLIENT_FILES". Stored on every run so dummy-data results can never be mistaken for
    /// client results.
    /// </summary>
    public required string DataSourceKind { get; set; }

    public bool UsesDummyData { get; set; }

    public string? DataSourceWarning { get; set; }

    /// <summary>Workbook path relative to the data root (never an absolute server path).</summary>
    public string? AssignmentsWorkbook { get; set; }

    /// <summary>CP-SAT status: OPTIMAL, FEASIBLE, INFEASIBLE, UNKNOWN, MODEL_INVALID or NOT_RUN.</summary>
    public string? SolverStatus { get; set; }

    public long? WallTimeMs { get; set; }

    public int TimeLimitSeconds { get; set; }

    public int Workers { get; set; }

    public int RandomSeed { get; set; }

    public double? ObjectiveValue { get; set; }

    /// <summary>Result of the independent validator (not the solver's own claim).</summary>
    public bool? HardConstraintsSatisfied { get; set; }

    public int? HardViolationCount { get; set; }

    public long? SoftConstraintPenalty { get; set; }

    public int? SectionsInScope { get; set; }

    public int? SectionsScheduled { get; set; }

    public int? TotalSlotsScheduled { get; set; }

    /// <summary>User-safe failure message (never a stack trace).</summary>
    public string? Error { get; set; }

    /// <summary>
    /// The complete JSON report exactly as the CLI writes it. Stored as PostgreSQL <c>json</c> (not <c>jsonb</c>) so the
    /// text — including key order — is returned unchanged by GET /api/timetable/result.
    /// </summary>
    public string? ReportJson { get; set; }

    /// <summary>The report's "solver" object (small; served by GET /api/timetable/jobs/{id} without reading the report).</summary>
    public string? SolverJson { get; set; }

    /// <summary>The report's "summary" object.</summary>
    public string? SummaryJson { get; set; }

    /// <summary>The report's diagnostic.relaxedHardRuleViolations (only for infeasible strict runs), served by /conflicts.</summary>
    public string? DiagnosticJson { get; set; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL's system column xmin.</summary>
    public uint Version { get; set; }

    public ICollection<TeachingAssignmentEntity> TeachingAssignments { get; set; } = new List<TeachingAssignmentEntity>();

    public ICollection<TimetableEntryEntity> TimetableEntries { get; set; } = new List<TimetableEntryEntity>();

    public ICollection<DataConflictEntity> DataConflicts { get; set; } = new List<DataConflictEntity>();

    public ICollection<SchedulingConflictEntity> SchedulingConflicts { get; set; } = new List<SchedulingConflictEntity>();
}
