using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;
public interface ITeacherService
{
    Task<TeacherTimetablesDto> GetAllAsync(CancellationToken cancellationToken);
}
