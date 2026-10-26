using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>Scoped implementation of <see cref="ISectionService"/>.</summary>
public sealed class SectionService : ISectionService
{
    private readonly ITimetableRepository _runs;
    private readonly ISectionRepository _sections;

    public SectionService(ITimetableRepository runs, ISectionRepository sections)
    {
        _runs = runs;
        _sections = sections;
    }

    public async Task<SectionTimetablesDto> GetAllAsync(CancellationToken cancellationToken)
    {
        var run = await LatestGenerationAsync(cancellationToken).ConfigureAwait(false);
        var lessons = await _sections.GetLessonsAsync(run.Id, null, cancellationToken).ConfigureAwait(false);
        return new SectionTimetablesDto(
            run.Success == true,
            run.Outcome,
            run.DataSourceKind,
            lessons.Count > 0 ? TimetableViewMapper.SectionWeeks(lessons) : null);
    }

    public async Task<SectionTimetableDto> GetAsync(string idOrDisplayName, CancellationToken cancellationToken)
    {
        var run = await LatestGenerationAsync(cancellationToken).ConfigureAwait(false);
        var section = await _sections.FindAsync(idOrDisplayName, cancellationToken).ConfigureAwait(false)
                      ?? throw new ResourceNotFoundException("Section not found.");

        if (await _sections.IsExcludedAsync(run.Id, section.DisplayName, cancellationToken).ConfigureAwait(false))
        {
            throw new ResourceNotFoundException($"{section.DisplayName} is excluded from scheduling; see dataConflicts.");
        }

        var lessons = await _sections.GetLessonsAsync(run.Id, section.ExternalId, cancellationToken).ConfigureAwait(false);
        if (lessons.Count == 0)
        {
            throw new ResourceNotFoundException($"No valid timetable was generated (outcome {run.Outcome}).");
        }

        return new SectionTimetableDto(section.DisplayName, section.ExternalId, run.Outcome, TimetableViewMapper.Week(lessons));
    }

    private async Task<RunRecord> LatestGenerationAsync(CancellationToken cancellationToken) =>
        await _runs.GetLatestCompletedRunAsync(RunKind.Generation, cancellationToken).ConfigureAwait(false)
        ?? throw new ResourceNotFoundException(TimetableViewMapper.NoGenerationMessage);
}
