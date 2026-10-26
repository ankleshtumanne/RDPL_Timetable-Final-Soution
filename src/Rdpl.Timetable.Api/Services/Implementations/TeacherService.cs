using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// Scoped implementation of <see cref="ITeacherService"/>. Builds the same structure as the report's teacherTimetables
/// (ReportBuilder.BuildTeacherTimetables): keys sorted ordinally, only days on which the teacher teaches (Monday first),
/// lessons by period. Two teachers with the same name are keyed "Name (CODE)".
/// </summary>
public sealed class TeacherService : ITeacherService
{
    private readonly ITimetableRepository _runs;
    private readonly ITeacherRepository _teachers;

    public TeacherService(ITimetableRepository runs, ITeacherRepository teachers)
    {
        _runs = runs;
        _teachers = teachers;
    }

    public async Task<TeacherTimetablesDto> GetAllAsync(CancellationToken cancellationToken)
    {
        var run = await _runs.GetLatestCompletedRunAsync(RunKind.Generation, cancellationToken).ConfigureAwait(false)
                  ?? throw new ResourceNotFoundException(TimetableViewMapper.NoGenerationMessage);

        var lessons = await _teachers.GetBookedLessonsAsync(run.Id, cancellationToken).ConfigureAwait(false);
        if (lessons.Count == 0)
        {
            return new TeacherTimetablesDto(run.Success == true, run.Outcome, run.DataSourceKind, null);
        }

        var byTeacher = lessons.GroupBy(l => l.TeacherCode!, StringComparer.OrdinalIgnoreCase).ToList();
        var nameCounts = byTeacher
            .GroupBy(g => g.First().TeacherName ?? g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var result = new SortedDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<TeacherLessonDto>>>(StringComparer.Ordinal);
        foreach (var teacher in byTeacher)
        {
            var name = teacher.First().TeacherName ?? teacher.Key;
            var key = nameCounts[name] > 1 ? $"{name} ({teacher.Key})" : name;
            var week = new Dictionary<string, IReadOnlyList<TeacherLessonDto>>(StringComparer.Ordinal);
            // Like the report: a day appears only when the teacher has at least one lesson on it.
            foreach (var day in teacher.GroupBy(l => l.Day).OrderBy(g => g.Key))
            {
                week[TimetableViewMapper.DayKey(day.Key)] = day
                    .OrderBy(l => l.Period)
                    .Select(l => new TeacherLessonDto(l.Period, l.Slot, l.SectionDisplayName, l.SubjectName))
                    .ToList();
            }

            result[key] = week;
        }

        return new TeacherTimetablesDto(run.Success == true, run.Outcome, run.DataSourceKind, result);
    }
}
