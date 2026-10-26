namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>
/// One entry of the report's dataConflicts[] (bad or excluded input rows). Section/subject/teacher are kept as text,
/// not foreign keys, because a conflict often describes a row whose section or teacher does not exist.
/// </summary>
public class DataConflictEntity
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public TimetableRunEntity Run { get; set; } = null!;

    /// <summary>e.g. UNASSIGNED_TEACHER, ZERO_WORKLOAD, DUMMY_DATA_IN_USE.</summary>
    public required string Type { get; set; }

    /// <summary>info, warning or error.</summary>
    public required string Severity { get; set; }

    public required string Message { get; set; }

    public string? SectionText { get; set; }

    public string? SubjectText { get; set; }

    public string? TeacherCode { get; set; }

    /// <summary>Source file and row, e.g. "TEACHER_CLASS_ALLOCATION.DUMMY.xlsx!Section Assignments:42".</summary>
    public string? Source { get; set; }

    /// <summary>Related Timetable-Engine-Rules.md id, e.g. T4, H10.</summary>
    public string? RuleId { get; set; }
}
