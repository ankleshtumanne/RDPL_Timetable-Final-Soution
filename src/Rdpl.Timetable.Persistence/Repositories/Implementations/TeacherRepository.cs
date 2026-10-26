using Microsoft.EntityFrameworkCore;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>EF Core / PostgreSQL implementation of <see cref="ITeacherRepository"/>. Read-only, no tracking.</summary>
public sealed class TeacherRepository : ITeacherRepository
{
    private readonly AppDbContext _db;

    public TeacherRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LessonRow>> GetBookedLessonsAsync(Guid runId, CancellationToken cancellationToken) =>
        await _db.TimetableEntries.AsNoTracking()
            .Where(e => e.RunId == runId && e.TeacherId != null && e.LessonType == "SUBJECT")
            .OrderBy(e => e.Id)
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
                e.Teacher!.Code,
                e.TeacherName,
                e.Instructors,
                e.Note))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
