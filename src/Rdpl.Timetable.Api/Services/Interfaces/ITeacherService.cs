using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>Teacher-wise timetables of the latest completed generation (read from the repository).</summary>
public interface ITeacherService
{
    /// <summary>Throws <c>ResourceNotFoundException</c> when no generation has completed.</summary>
    Task<TeacherTimetablesDto> GetAllAsync(CancellationToken cancellationToken);
}
