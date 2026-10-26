using System.Globalization;
using System.Text.Json;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Models;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>Stored rows → the report DTOs (same types the CLI report uses), so the JSON shape cannot drift.</summary>
internal static class TimetableViewMapper
{
    public const string NoGenerationMessage = "No timetable has been generated yet. Call POST /api/timetable/generate and poll the job.";

    public static string DayKey(TimetableDay day) => day.ToString().ToUpperInvariant();

    public static SectionLessonDto ToSectionLesson(LessonRow l) => new(
        l.Period,
        l.Slot,
        l.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        l.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        l.SubjectName,
        l.LessonType,
        l.TeacherCode,
        l.TeacherName,
        l.Instructors.Count > 0 ? l.Instructors : null, // the report writes no "instructors" when there are none
        l.Note);

    /// <summary>Groups lessons section → day → lessons, keeping the stored (report) order at every level.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>> SectionWeeks(IEnumerable<LessonRow> lessons)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>>>(StringComparer.Ordinal);
        foreach (var section in lessons.GroupBy(l => l.SectionDisplayName, StringComparer.Ordinal))
        {
            result[section.Key] = Week(section);
        }

        return result;
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<SectionLessonDto>> Week(IEnumerable<LessonRow> lessons)
    {
        var days = new Dictionary<string, IReadOnlyList<SectionLessonDto>>(StringComparer.Ordinal);
        foreach (var day in lessons.GroupBy(l => l.Day))
        {
            days[DayKey(day.Key)] = day.Select(ToSectionLesson).ToList();
        }

        return days;
    }

    public static DataConflictDto ToDto(DataConflictData c) => new(c.Type, c.Severity, c.Message, c.Section, c.Subject, c.TeacherCode, c.Source, c.RuleId);

    public static SchedulingConflictDto ToDto(SchedulingConflictData c) =>
        new(c.Code, c.RuleId, c.Message, c.ProvesInfeasibility, c.Sections.Count > 0 ? c.Sections : null, c.TeacherCode);

    public static JsonElement? Parse(string? json, string? property = null)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        if (property is null)
        {
            return document.RootElement.Clone();
        }

        return document.RootElement.TryGetProperty(property, out var value) ? value.Clone() : null;
    }
}
