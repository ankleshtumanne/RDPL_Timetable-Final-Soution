using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>
/// Use cases behind <c>TimetableController</c>. Validates requests, calls the existing Core pipeline
/// (<c>TimetableEngine</c>: loader → CP-SAT → independent validator → report) and stores results through
/// <c>ITimetableRepository</c>. Throws <c>RequestValidationException</c>, <c>GenerationInProgressException</c> or
/// <c>DataInputException</c>; the centralized exception handler maps them to HTTP responses.
/// </summary>
public interface ITimetableService
{
    /// <summary>Loads the configured files, normalizes them and runs the pre-solve analysis (no solver). Stored as a run.</summary>
    Task<LoadDataResponseDto> LoadDataAsync(GenerateRequest? request, CancellationToken cancellationToken);

    /// <summary>Creates a run and starts the CP-SAT solve in the background. Returns immediately in state RUNNING.</summary>
    Task<JobDto> StartGenerationAsync(GenerateRequest? request, CancellationToken cancellationToken);

    Task<JobDto?> GetJobAsync(Guid runId, CancellationToken cancellationToken);

    /// <summary>Full JSON report of the most recently completed generation, exactly as stored, or null.</summary>
    Task<string?> GetLatestResultJsonAsync(CancellationToken cancellationToken);

    Task<PagedResultDto<RunListItemDto>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken);
}
