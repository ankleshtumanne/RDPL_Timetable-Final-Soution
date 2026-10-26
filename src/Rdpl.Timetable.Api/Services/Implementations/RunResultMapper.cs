using System.Globalization;
using System.Text.Json;
using Rdpl.Timetable.Configuration;
using Rdpl.Timetable.Data;
using Rdpl.Timetable.Models;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Models;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// Maps one Core run (input dataset + report) to the repository's <see cref="RunResult"/>. Pure function: no I/O.
/// Lessons are included ONLY when the report is successful AND the independent validator found no hard violation;
/// diagnostic (relaxed) timetables are never stored as lessons.
/// </summary>
public static class RunResultMapper
{
    public static RunResult Map(Guid runId, LoadedDataset dataset, TimetableRun run, RuleOptions rules, DateTimeOffset completedUtc)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(rules);
        var report = run.Report;

        var sections = dataset.Sections
            .Select(s => new SectionData(Clip(s.Id, 64)!, Clip(s.DisplayName, 200)!, Clip(s.Grade, 32)!, Clip(s.SectionName, 32)!, Clip(s.CurriculumKey, 32)!, s.Capacity, s.IsPrePrimary))
            .ToList();
        var sectionByText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in dataset.Sections)
        {
            sectionByText.TryAdd(s.DisplayName.Trim(), s.Id);
            sectionByText.TryAdd(s.Id.Trim(), s.Id);
        }

        var teachers = new Dictionary<string, TeacherData>(StringComparer.Ordinal);
        void AddTeacher(string? code, string? name)
        {
            var c = Clip(code, 64);
            if (c is null)
            {
                return;
            }

            teachers.TryAdd(c, new TeacherData(c, Clip(name, 200) ?? c, rules.PlaceholderTeacherCodes.Contains(c)));
        }

        var subjects = new List<SubjectData>();
        foreach (var item in dataset.Curricula.Values.SelectMany(c => c.Items))
        {
            subjects.Add(new SubjectData(Clip(item.Name, 200)!, item.Type == CurriculumItemType.ActivityGroup ? SubjectKind.ActivityGroup : SubjectKind.Subject));
        }

        var assignments = new List<AssignmentData>(dataset.AssignmentRows.Count);
        foreach (var row in dataset.AssignmentRows)
        {
            var subject = Clip(row.SubjectName, 200);
            var teacherCode = Clip(row.TeacherCode, 64);
            AddTeacher(teacherCode, row.TeacherName);
            if (subject is not null)
            {
                subjects.Add(new SubjectData(subject, SubjectKind.Unknown));
            }

            assignments.Add(new AssignmentData(
                SectionExternalId: sectionByText.TryGetValue(row.SectionText.Trim(), out var sectionId) ? sectionId : null,
                SubjectName: subject,
                TeacherCode: teacherCode,
                SectionText: Clip(row.SectionText, 200) ?? string.Empty,
                SubjectText: subject ?? string.Empty,
                TeacherCodeText: teacherCode ?? string.Empty,
                TeacherNameText: Clip(row.TeacherName, 200) ?? string.Empty,
                WorkloadPerWeek: row.WorkloadPerWeek,
                SourceFile: Clip(Path.GetFileName(row.SourceFile), 260) ?? string.Empty,
                SourceSheet: Clip(row.Sheet, 64) ?? string.Empty,
                SourceRow: Math.Max(1, row.RowNumber),
                DataOrigin: Clip(row.DataOrigin, 64) ?? string.Empty,
                IsDummy: row.IsDummy));
        }

        var storeLessons = report.Success && report.Validation?.HardConstraintsSatisfied == true && report.SectionTimetables is not null;
        var lessons = new List<LessonData>();
        if (storeLessons)
        {
            var sectionByDisplay = dataset.Sections.ToDictionary(s => s.DisplayName, s => s.Id, StringComparer.Ordinal);
            foreach (var (sectionName, week) in report.SectionTimetables!)
            {
                var sectionId = sectionByDisplay.TryGetValue(sectionName, out var id)
                    ? id
                    : throw new InvalidOperationException($"Report section '{sectionName}' is not in sections.json.");
                foreach (var (dayText, dayLessons) in week)
                {
                    var day = Enum.Parse<TimetableDay>(dayText, ignoreCase: true);
                    foreach (var l in dayLessons)
                    {
                        AddTeacher(l.TeacherCode, l.TeacherName);
                        var subject = Clip(l.Subject, 200)!;
                        subjects.Add(new SubjectData(subject, l.Type == "ACTIVITY" ? SubjectKind.ActivityGroup : SubjectKind.Subject));
                        lessons.Add(new LessonData(
                            sectionId,
                            subject,
                            Clip(l.TeacherCode, 64),
                            Clip(l.TeacherName, 200),
                            day,
                            l.Period,
                            Clip(l.Slot, 16)!,
                            TimeOnly.ParseExact(l.Start, "HH:mm", CultureInfo.InvariantCulture),
                            TimeOnly.ParseExact(l.End, "HH:mm", CultureInfo.InvariantCulture),
                            l.Type,
                            l.Instructors?.ToList() ?? new List<string>(),
                            l.Note));
                    }
                }
            }
        }

        return new RunResult
        {
            RunId = runId,
            State = RunState.Completed,
            CompletedUtc = completedUtc,
            Outcome = report.Outcome,
            Success = report.Success,
            DataSourceKind = Clip(report.DataSource.Kind, 64)!,
            UsesDummyData = dataset.AssignmentsAreDummy,
            DataSourceWarning = report.DataSource.Warning,
            AssignmentsWorkbook = Clip(report.DataSource.AssignmentsWorkbook, 260),
            SolverStatus = Clip(report.Solver.Status, 32),
            WallTimeMs = report.Solver.WallTimeMs,
            ObjectiveValue = report.Solver.ObjectiveValue,
            HardConstraintsSatisfied = report.Validation?.HardConstraintsSatisfied,
            HardViolationCount = report.Validation?.HardViolationCount,
            SoftConstraintPenalty = report.Validation?.SoftConstraintPenalty,
            SectionsInScope = report.Summary.SectionsInScope,
            SectionsScheduled = report.Summary.SectionsScheduled,
            TotalSlotsScheduled = report.Summary.TotalSlotsScheduled,
            ReportJson = report.ToJson(),
            SolverJson = JsonSerializer.Serialize(report.Solver, TimetableReport.JsonOptions),
            SummaryJson = JsonSerializer.Serialize(report.Summary, TimetableReport.JsonOptions),
            DiagnosticJson = report.Diagnostic?.RelaxedHardRuleViolations is { } violations
                ? JsonSerializer.Serialize(violations, TimetableReport.JsonOptions)
                : null,
            Sections = sections,
            Teachers = teachers.Values.ToList(),
            Subjects = subjects,
            Assignments = assignments,
            Lessons = lessons,
            DataConflicts = report.DataConflicts
                .Select(c => new DataConflictData(Clip(c.Type, 64)!, Clip(c.Severity, 16)!, c.Message, Clip(c.Section, 200), Clip(c.Subject, 200), Clip(c.TeacherCode, 64), Clip(c.Source, 260), Clip(c.RuleId, 32)))
                .ToList(),
            SchedulingConflicts = report.SchedulingConflicts
                .Select(c => new SchedulingConflictData(Clip(c.Code, 64)!, Clip(c.RuleId, 64)!, c.Message, c.ProvesInfeasibility, c.Sections ?? Array.Empty<string>(), Clip(c.TeacherCode, 64)))
                .ToList(),
        };
    }

    /// <summary>Trims and bounds a value to its column length; empty becomes null.</summary>
    public static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
