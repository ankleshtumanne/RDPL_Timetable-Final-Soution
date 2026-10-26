using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Repositories.Models;

/// <summary>
/// One stored lesson with its section, subject and teacher resolved. Repositories return lessons in the order they were
/// stored, which is the report's order (section → day → period).
/// </summary>
public sealed record LessonRow(
    string SectionExternalId,
    string SectionDisplayName,
    TimetableDay Day,
    int Period,
    string Slot,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SubjectName,
    string LessonType,
    string? TeacherCode,
    string? TeacherName,
    IReadOnlyList<string> Instructors,
    string? Note);

/// <summary>A section from the master table.</summary>
public sealed record SectionRecord(string ExternalId, string DisplayName);
