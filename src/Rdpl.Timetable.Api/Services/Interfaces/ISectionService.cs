using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>Section-wise timetables of the latest completed generation (read from the repository).</summary>
public interface ISectionService
{
    /// <summary>Throws <c>ResourceNotFoundException</c> when no generation has completed.</summary>
    Task<SectionTimetablesDto> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One section by sections.json id or display name. Throws <c>ResourceNotFoundException</c> when there is no
    /// generation, the section is unknown, excluded from scheduling, or has no valid timetable.
    /// </summary>
    Task<SectionTimetableDto> GetAsync(string idOrDisplayName, CancellationToken cancellationToken);
}
