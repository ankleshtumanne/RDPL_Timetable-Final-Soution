using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;
public interface IConflictService
{
    Task<ConflictsDto> GetLatestAsync(CancellationToken cancellationToken);
}
