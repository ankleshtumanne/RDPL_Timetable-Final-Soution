using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;
public interface ISectionService
{
    Task<SectionTimetablesDto> GetAllAsync(CancellationToken cancellationToken);
    Task<SectionTimetableDto> GetAsync(string idOrDisplayName, CancellationToken cancellationToken);
}
