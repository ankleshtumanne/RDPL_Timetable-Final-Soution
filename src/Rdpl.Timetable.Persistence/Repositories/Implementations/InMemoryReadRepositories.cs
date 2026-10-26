using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>In-memory <see cref="ISectionRepository"/> over <see cref="InMemoryRunStore"/>.</summary>
public sealed class InMemorySectionRepository : ISectionRepository
{
    private readonly InMemoryRunStore _store;

    public InMemorySectionRepository(InMemoryRunStore store)
    {
        _store = store;
    }

    public Task<SectionRecord?> FindAsync(string idOrDisplayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrDisplayName);
        var key = idOrDisplayName.Trim();
        var sections = _store.Snapshot()
            .Where(r => r.Result is not null)
            .OrderByDescending(r => r.Record.CreatedUtc)
            .SelectMany(r => r.Result!.Sections)
            .ToList();
        var match = sections.FirstOrDefault(s => string.Equals(s.ExternalId, key, StringComparison.OrdinalIgnoreCase))
                    ?? sections.FirstOrDefault(s => string.Equals(s.DisplayName, key, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(match is null ? null : new SectionRecord(match.ExternalId, match.DisplayName));
    }

    public Task<IReadOnlyList<LessonRow>> GetLessonsAsync(Guid runId, string? sectionExternalId, CancellationToken cancellationToken)
    {
        IReadOnlyList<LessonRow> rows = InMemoryLessons.Rows(_store.Get(runId)?.Result)
            .Where(l => sectionExternalId is null || l.SectionExternalId == sectionExternalId)
            .ToList();
        return Task.FromResult(rows);
    }

    public Task<bool> IsExcludedAsync(Guid runId, string sectionDisplayName, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Get(runId)?.Result?.DataConflicts
            .Any(c => ConflictTypes.IsSectionExclusion(c.Type) && c.Section == sectionDisplayName) ?? false);
}

/// <summary>In-memory <see cref="ITeacherRepository"/> over <see cref="InMemoryRunStore"/>.</summary>
public sealed class InMemoryTeacherRepository : ITeacherRepository
{
    private readonly InMemoryRunStore _store;

    public InMemoryTeacherRepository(InMemoryRunStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<LessonRow>> GetBookedLessonsAsync(Guid runId, CancellationToken cancellationToken)
    {
        IReadOnlyList<LessonRow> rows = InMemoryLessons.Rows(_store.Get(runId)?.Result)
            .Where(l => l.TeacherCode is not null && l.LessonType == "SUBJECT")
            .ToList();
        return Task.FromResult(rows);
    }
}

/// <summary>In-memory <see cref="IConflictRepository"/> over <see cref="InMemoryRunStore"/>.</summary>
public sealed class InMemoryConflictRepository : IConflictRepository
{
    private readonly InMemoryRunStore _store;

    public InMemoryConflictRepository(InMemoryRunStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<DataConflictData>> GetDataConflictsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Get(runId)?.Result?.DataConflicts ?? Array.Empty<DataConflictData>());

    public Task<IReadOnlyList<SchedulingConflictData>> GetSchedulingConflictsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Get(runId)?.Result?.SchedulingConflicts ?? Array.Empty<SchedulingConflictData>());
}

internal static class InMemoryLessons
{
    /// <summary>Same projection as the EF repositories (master section name, subject name as stored).</summary>
    public static IEnumerable<LessonRow> Rows(RunResult? result)
    {
        if (result is null)
        {
            yield break;
        }

        var sections = result.Sections.ToDictionary(s => s.ExternalId, s => s.DisplayName, StringComparer.Ordinal);
        foreach (var l in result.Lessons)
        {
            yield return new LessonRow(l.SectionExternalId, sections.TryGetValue(l.SectionExternalId, out var name) ? name : l.SectionExternalId,
                l.Day, l.Period, l.Slot, l.StartTime, l.EndTime, l.SubjectName, l.LessonType, l.TeacherCode, l.TeacherName, l.Instructors, l.Note);
        }
    }
}
