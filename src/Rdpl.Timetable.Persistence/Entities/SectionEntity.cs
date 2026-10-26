namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>
/// Master record for a class section (source: sections.json). Upserted by <see cref="ExternalId"/> on every run, so
/// timetable entries from different runs point at the same row.
/// </summary>
public class SectionEntity : IAuditedEntity
{
    public int Id { get; set; }

    /// <summary>The id used in sections.json, e.g. "class-10-lily". Unique.</summary>
    public required string ExternalId { get; set; }

    /// <summary>e.g. "Grade 10 - Lily".</summary>
    public required string DisplayName { get; set; }

    public required string Grade { get; set; }

    public required string SectionName { get; set; }

    /// <summary>Links to the curriculum in CLASS_WISE_SUBJECTS.md, e.g. "Class 10".</summary>
    public required string CurriculumKey { get; set; }

    public int Capacity { get; set; }

    /// <summary>Rule P4: Pre-Nursery/Nursery/Junior KG/Senior KG are excluded from generation.</summary>
    public bool IsPrePrimary { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<TeachingAssignmentEntity> TeachingAssignments { get; set; } = new List<TeachingAssignmentEntity>();

    public ICollection<TimetableEntryEntity> TimetableEntries { get; set; } = new List<TimetableEntryEntity>();
}
