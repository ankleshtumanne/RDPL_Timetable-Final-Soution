namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>Master record for a teacher (source: assignment workbook + roster). Upserted by <see cref="Code"/>.</summary>
public class TeacherEntity : IAuditedEntity
{
    public int Id { get; set; }

    /// <summary>Employee code, e.g. "RVS115". Unique.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>True for the UNASSIGNED-TT placeholder. Its rows are stored for traceability but never scheduled (DATA-1/T4).</summary>
    public bool IsPlaceholder { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<TeachingAssignmentEntity> TeachingAssignments { get; set; } = new List<TeachingAssignmentEntity>();

    public ICollection<TimetableEntryEntity> TimetableEntries { get; set; } = new List<TimetableEntryEntity>();
}
