using Microsoft.EntityFrameworkCore;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>EF Core / PostgreSQL implementation of <see cref="ISectionRepository"/>. Read-only, no tracking.</summary>
public sealed class SectionRepository : ISectionRepository
{
    private readonly AppDbContext _db;

    public SectionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SectionRecord?> FindAsync(string idOrDisplayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrDisplayName);
        var key = idOrDisplayName.Trim().ToLowerInvariant();
        var candidates = await _db.Sections.AsNoTracking()
            .Where(s => s.ExternalId.ToLower() == key || s.DisplayName.ToLower() == key)
            .Select(s => new SectionRecord(s.ExternalId, s.DisplayName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Same precedence as the original endpoint: the sections.json id first, then the display name.
        return candidates.FirstOrDefault(s => string.Equals(s.ExternalId, key, StringComparison.OrdinalIgnoreCase))
               ?? candidates.FirstOrDefault();
    }

    public async Task<IReadOnlyList<LessonRow>> GetLessonsAsync(Guid runId, string? sectionExternalId, CancellationToken cancellationToken)
    {
        var query = _db.TimetableEntries.AsNoTracking().Where(e => e.RunId == runId);
        if (sectionExternalId is not null)
        {
            query = query.Where(e => e.Section.ExternalId == sectionExternalId);
        }

        return await query
            .OrderBy(e => e.Id) // insertion order = report order (section → day → period)
            .Select(e => new LessonRow(
                e.Section.ExternalId,
                e.Section.DisplayName,
                e.Day,
                e.Period,
                e.Slot,
                e.StartTime,
                e.EndTime,
                e.Subject.Name,
                e.LessonType,
                e.Teacher != null ? e.Teacher.Code : null,
                e.TeacherName,
                e.Instructors,
                e.Note))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> IsExcludedAsync(Guid runId, string sectionDisplayName, CancellationToken cancellationToken) =>
        _db.DataConflicts.AsNoTracking().AnyAsync(
            c => c.RunId == runId
                 && (c.Type == ConflictTypes.SectionExcludedPrePrimary || c.Type == ConflictTypes.SectionExcludedNoCurriculum)
                 && c.SectionText == sectionDisplayName,
            cancellationToken);
}
