using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Interfaces;

/// <summary>Section master data and section-wise lessons of a run.</summary>
public interface ISectionRepository
{
    /// <summary>Finds a section by its sections.json id ("class-10-lily") or display name ("Grade 10 - Lily"), case-insensitive.</summary>
    Task<SectionRecord?> FindAsync(string idOrDisplayName, CancellationToken cancellationToken);

    /// <summary>Lessons of a run in stored order; all sections, or one when <paramref name="sectionExternalId"/> is given.</summary>
    Task<IReadOnlyList<LessonRow>> GetLessonsAsync(Guid runId, string? sectionExternalId, CancellationToken cancellationToken);

    /// <summary>True when the run excluded this section from scheduling (P3/P4 data conflicts).</summary>
    Task<bool> IsExcludedAsync(Guid runId, string sectionDisplayName, CancellationToken cancellationToken);
}
