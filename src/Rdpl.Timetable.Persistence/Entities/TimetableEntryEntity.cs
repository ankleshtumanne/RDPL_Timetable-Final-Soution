namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>
/// One generated lesson: section × day × period. Only written for runs whose timetable passed the independent
/// validator. Two unique indexes re-check H3 (one lesson per section slot) and H1 (no teacher double-booking)
/// inside the database as a last line of defence.
/// </summary>
public class TimetableEntryEntity
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public TimetableRunEntity Run { get; set; } = null!;

    public int SectionId { get; set; }

    public SectionEntity Section { get; set; } = null!;

    public int SubjectId { get; set; }

    public SubjectEntity Subject { get; set; } = null!;

    /// <summary>
    /// The formally assigned teacher (T2). Null for activity groups (instructors are not teacher-booked, T6/AC8) and for
    /// slots reserved for unstaffed subjects (DATA-1).
    /// </summary>
    public int? TeacherId { get; set; }

    public TeacherEntity? Teacher { get; set; }

    /// <summary>
    /// Teacher name exactly as shown in this run's report (snapshot). The master row's name can change in later runs;
    /// a generated timetable must not.
    /// </summary>
    public string? TeacherName { get; set; }

    public TimetableDay Day { get; set; }

    /// <summary>Teaching period number from bell-schedule.json (1–8 Mon–Thu, 1–10 Fri–Sat).</summary>
    public short Period { get; set; }

    /// <summary>Bell slot label, e.g. "1A".</summary>
    public required string Slot { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    /// <summary>SUBJECT, ACTIVITY or RESERVED_UNSTAFFED (same values as the JSON report).</summary>
    public required string LessonType { get; set; }

    /// <summary>Activity instructor codes (informational; not teacher-booked).</summary>
    public List<string> Instructors { get; set; } = new();

    public string? Note { get; set; }
}
