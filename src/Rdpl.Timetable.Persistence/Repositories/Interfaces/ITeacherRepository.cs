using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Interfaces;

/// <summary>Teacher-wise lessons of a run.</summary>
public interface ITeacherRepository
{
    /// <summary>
    /// Lessons that book a teacher (subject lessons with an assigned teacher; activities are not teacher-booked, T6/AC8),
    /// in stored order.
    /// </summary>
    Task<IReadOnlyList<LessonRow>> GetBookedLessonsAsync(Guid runId, CancellationToken cancellationToken);
}
