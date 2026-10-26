namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>Master record for a subject or activity group. Upserted by <see cref="NormalizedName"/>.</summary>
public class SubjectEntity : IAuditedEntity
{
    public int Id { get; set; }

    /// <summary>Name as written in the source file, e.g. "English Language" or "Games / Library".</summary>
    public required string Name { get; set; }

    /// <summary>Trimmed, upper-invariant <see cref="Name"/>; unique. Use <see cref="Normalize"/>.</summary>
    public required string NormalizedName { get; set; }

    public SubjectKind Kind { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<TeachingAssignmentEntity> TeachingAssignments { get; set; } = new List<TeachingAssignmentEntity>();

    public ICollection<TimetableEntryEntity> TimetableEntries { get; set; } = new List<TimetableEntryEntity>();

    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }
}
