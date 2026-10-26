namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>
/// One section-level assignment row exactly as read for a run (snapshot of TEACHER_CLASS_ALLOCATION.xlsx).
/// Rows are stored per run because the input file can change between runs (dummy today, client file later).
/// Raw text columns are always kept; the foreign keys are null when the row could not be resolved
/// (e.g. unknown section), so bad rows are preserved instead of silently dropped.
/// </summary>
public class TeachingAssignmentEntity
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public TimetableRunEntity Run { get; set; } = null!;

    public int? SectionId { get; set; }

    public SectionEntity? Section { get; set; }

    public int? SubjectId { get; set; }

    public SubjectEntity? Subject { get; set; }

    public int? TeacherId { get; set; }

    public TeacherEntity? Teacher { get; set; }

    public required string SectionText { get; set; }

    public required string SubjectText { get; set; }

    public required string TeacherCode { get; set; }

    public required string TeacherName { get; set; }

    /// <summary>workload_per_week as written; null when the cell was empty or not a number.</summary>
    public int? WorkloadPerWeek { get; set; }

    /// <summary>File name only, e.g. "TEACHER_CLASS_ALLOCATION.DUMMY.xlsx".</summary>
    public required string SourceFile { get; set; }

    public required string SourceSheet { get; set; }

    public int SourceRow { get; set; }

    /// <summary>Origin label from the workbook (contains "DUMMY" for generated sample rows).</summary>
    public required string DataOrigin { get; set; }

    public bool IsDummy { get; set; }
}
