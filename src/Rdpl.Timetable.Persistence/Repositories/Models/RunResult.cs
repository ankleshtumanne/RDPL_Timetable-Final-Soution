using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Repositories.Models;

/// <summary>
/// Everything a finished run produced, in persistence terms. Master data is identified by natural keys
/// (section external id, teacher code, normalized subject name); the repository resolves them to database ids.
/// Built by the API service layer from the Core report, so this project needs no reference to the solver.
/// </summary>
public sealed class RunResult
{
    public required Guid RunId { get; init; }

    public required RunState State { get; init; }

    public required DateTimeOffset CompletedUtc { get; init; }

    public string? Outcome { get; init; }

    public bool? Success { get; init; }

    public required string DataSourceKind { get; init; }

    public bool UsesDummyData { get; init; }

    public string? DataSourceWarning { get; init; }

    public string? AssignmentsWorkbook { get; init; }

    public string? SolverStatus { get; init; }

    public long? WallTimeMs { get; init; }

    public double? ObjectiveValue { get; init; }

    public bool? HardConstraintsSatisfied { get; init; }

    public int? HardViolationCount { get; init; }

    public long? SoftConstraintPenalty { get; init; }

    public int? SectionsInScope { get; init; }

    public int? SectionsScheduled { get; init; }

    public int? TotalSlotsScheduled { get; init; }

    public required string ReportJson { get; init; }

    public string? SolverJson { get; init; }

    public string? SummaryJson { get; init; }

    public string? DiagnosticJson { get; init; }

    public IReadOnlyList<SectionData> Sections { get; init; } = Array.Empty<SectionData>();

    public IReadOnlyList<TeacherData> Teachers { get; init; } = Array.Empty<TeacherData>();

    public IReadOnlyList<SubjectData> Subjects { get; init; } = Array.Empty<SubjectData>();

    public IReadOnlyList<AssignmentData> Assignments { get; init; } = Array.Empty<AssignmentData>();

    /// <summary>Generated lessons. Must be empty unless the timetable passed the independent validator.</summary>
    public IReadOnlyList<LessonData> Lessons { get; init; } = Array.Empty<LessonData>();

    public IReadOnlyList<DataConflictData> DataConflicts { get; init; } = Array.Empty<DataConflictData>();

    public IReadOnlyList<SchedulingConflictData> SchedulingConflicts { get; init; } = Array.Empty<SchedulingConflictData>();
}

public sealed record SectionData(string ExternalId, string DisplayName, string Grade, string SectionName, string CurriculumKey, int Capacity, bool IsPrePrimary);

public sealed record TeacherData(string Code, string Name, bool IsPlaceholder);

/// <summary><paramref name="Name"/> is normalized with <see cref="SubjectEntity.Normalize"/> by the repository.</summary>
public sealed record SubjectData(string Name, SubjectKind Kind);

/// <summary>Foreign keys are given as natural keys; null when the row could not be resolved (kept as raw text).</summary>
public sealed record AssignmentData(
    string? SectionExternalId,
    string? SubjectName,
    string? TeacherCode,
    string SectionText,
    string SubjectText,
    string TeacherCodeText,
    string TeacherNameText,
    int? WorkloadPerWeek,
    string SourceFile,
    string SourceSheet,
    int SourceRow,
    string DataOrigin,
    bool IsDummy);

public sealed record LessonData(
    string SectionExternalId,
    string SubjectName,
    string? TeacherCode,
    string? TeacherName,
    TimetableDay Day,
    int Period,
    string Slot,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string LessonType,
    IReadOnlyList<string> Instructors,
    string? Note);

public sealed record DataConflictData(string Type, string Severity, string Message, string? Section, string? Subject, string? TeacherCode, string? Source, string? RuleId);

public sealed record SchedulingConflictData(string Code, string RuleId, string Message, bool ProvesInfeasibility, IReadOnlyList<string> Sections, string? TeacherCode);
