using System.Text.Json;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Api.DTOs;

// Response shapes of the section / teacher / conflict endpoints. Property names and nested types are exactly those
// of the original endpoints (and of the CLI report), so existing clients see the same JSON.

/// <summary>GET /api/section (alias /api/timetable/sections). Section → day → lessons.</summary>
public sealed record SectionTimetablesDto(
    bool Success,
    string? Outcome,
    string DataSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>>? SectionTimetables);

/// <summary>GET /api/section/{id} (alias /api/timetable/sections/{id}).</summary>
public sealed record SectionTimetableDto(
    string Section,
    string Id,
    string? Outcome,
    IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>> Timetable);

/// <summary>GET /api/teacher (alias /api/timetable/teachers). Teacher name → day → lessons.</summary>
public sealed record TeacherTimetablesDto(
    bool Success,
    string? Outcome,
    string DataSource,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<TeacherLessonDto>>>? TeacherTimetables);

/// <summary>GET /api/conflict (alias /api/timetable/conflicts).</summary>
public sealed record ConflictsDto(
    string? Outcome,
    JsonElement? DataConflictCounts,
    IReadOnlyList<DataConflictDto> DataConflicts,
    IReadOnlyList<SchedulingConflictDto> SchedulingConflicts,
    JsonElement? Diagnostic);
