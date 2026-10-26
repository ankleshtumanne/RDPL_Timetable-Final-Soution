using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>Data and scheduling conflicts of the latest completed generation, or else the latest data validation.</summary>
public interface IConflictService
{
    /// <summary>Throws <c>ResourceNotFoundException</c> when nothing has been loaded or generated.</summary>
    Task<ConflictsDto> GetLatestAsync(CancellationToken cancellationToken);
}
