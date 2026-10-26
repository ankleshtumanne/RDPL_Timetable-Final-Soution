using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// Scoped implementation of <see cref="IConflictService"/>. Same precedence as the original endpoint: the latest
/// completed generation if there is one, otherwise the latest data validation (load-data).
/// </summary>
public sealed class ConflictService : IConflictService
{
    private readonly ITimetableRepository _runs;
    private readonly IConflictRepository _conflicts;

    public ConflictService(ITimetableRepository runs, IConflictRepository conflicts)
    {
        _runs = runs;
        _conflicts = conflicts;
    }

    public async Task<ConflictsDto> GetLatestAsync(CancellationToken cancellationToken)
    {
        var run = await _runs.GetLatestCompletedRunAsync(RunKind.Generation, cancellationToken).ConfigureAwait(false)
                  ?? await _runs.GetLatestCompletedRunAsync(RunKind.DataValidation, cancellationToken).ConfigureAwait(false)
                  ?? throw new ResourceNotFoundException("Nothing has been loaded yet. Call POST /api/timetable/load-data or /generate first.");

        var data = await _conflicts.GetDataConflictsAsync(run.Id, cancellationToken).ConfigureAwait(false);
        var scheduling = await _conflicts.GetSchedulingConflictsAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return new ConflictsDto(
            run.Outcome,
            TimetableViewMapper.Parse(run.SummaryJson, "dataConflictCounts"),
            data.Select(TimetableViewMapper.ToDto).ToList(),
            scheduling.Select(TimetableViewMapper.ToDto).ToList(),
            TimetableViewMapper.Parse(run.DiagnosticJson));
    }
}
