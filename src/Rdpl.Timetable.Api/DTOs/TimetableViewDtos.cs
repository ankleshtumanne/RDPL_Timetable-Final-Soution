using System.Text.Json;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Api.DTOs;
public sealed record SectionTimetablesDto(
    bool Success,
    string? Outcome,
    string DataSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>>? SectionTimetables);

public sealed record SectionTimetableDto(
    string Section,
    string Id,
    string? Outcome,
    IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>> Timetable);

public sealed record TeacherTimetablesDto(
    bool Success,
    string? Outcome,
    string DataSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<TeacherLessonDto>>>? TeacherTimetables);

public sealed record ConflictsDto(
    string? Outcome,
    JsonElement? DataConflictCounts,
    IReadOnlyList<DataConflictDto> DataConflicts,
    IReadOnlyList<SchedulingConflictDto> SchedulingConflicts,
    JsonElement? Diagnostic);
