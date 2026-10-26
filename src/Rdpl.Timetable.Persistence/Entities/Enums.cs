namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>What a persisted run did. Stored as text (e.g. "DataValidation").</summary>
public enum RunKind
{
    /// <summary>Load + normalize + pre-solve analysis only (POST /load-data).</summary>
    DataValidation,

    /// <summary>Full pipeline including the CP-SAT solve and independent validation (POST /generate).</summary>
    Generation,
}

/// <summary>Lifecycle of a run. Stored as text.</summary>
public enum RunState
{
    Running,
    Completed,
    Failed,
}

/// <summary>Curriculum item type from CLASS_WISE_SUBJECTS.md. Stored as text.</summary>
public enum SubjectKind
{
    Subject,
    ActivityGroup,

    /// <summary>Name seen only in an assignment row or conflict, not in the curriculum.</summary>
    Unknown,
}

/// <summary>Teaching day from bell-schedule.json. Stored as upper-case text to match the JSON report ("MONDAY").</summary>
public enum TimetableDay
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
}
