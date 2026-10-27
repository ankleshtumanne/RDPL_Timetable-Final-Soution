using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;
public interface ITimetableService
{
    Task<LoadDataResponseDto> LoadDataAsync(GenerateRequest? request, CancellationToken cancellationToken);
    Task<JobDto> StartGenerationAsync(GenerateRequest? request, CancellationToken cancellationToken);

    Task<JobDto?> GetJobAsync(Guid runId, CancellationToken cancellationToken);
    Task<string?> GetLatestResultJsonAsync(CancellationToken cancellationToken);
    Task<PagedResultDto<RunListItemDto>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken);
}
