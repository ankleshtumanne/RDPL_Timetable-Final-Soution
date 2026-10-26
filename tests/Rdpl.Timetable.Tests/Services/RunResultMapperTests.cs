using Rdpl.Timetable.Api.Services.Implementations;
using Rdpl.Timetable.Configuration;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Tests.TestSupport;

namespace Rdpl.Timetable.Tests.Services;

/// <summary>Core run → repository RunResult. Pure mapping, no database.</summary>
public sealed class RunResultMapperTests
{
    [Fact]
    public void Validated_timetable_maps_every_lesson_master_row_and_conflict()
    {
        var synth = SmallSchool.Build();
        var dataset = synth.Dataset();
        var run = TimetableEngine.Run(dataset, new RuleOptions(), Synth.Fast(), solve: true);
        Assert.True(run.Report.Success, run.Report.Outcome);

        var result = RunResultMapper.Map(Guid.NewGuid(), dataset, run, new RuleOptions(), DateTimeOffset.UtcNow);

        Assert.Equal(16, result.Lessons.Count); // 2 sections × 8 slots
        Assert.Equal(run.Report.Summary.TotalCellsFilled, result.Lessons.Count);
        Assert.Equal(run.Report.ToJson(), result.ReportJson);
        Assert.Equal(run.Report.DataConflicts.Count, result.DataConflicts.Count);
        Assert.Equal(dataset.AssignmentRows.Count, result.Assignments.Count);
        Assert.Equal(new[] { "class-4-lily", "class-4-lavender" }, result.Sections.Select(s => s.ExternalId));

        Assert.Contains(result.Teachers, t => t.Code == "T1" && t.Name == "Asha Teacher" && !t.IsPlaceholder);
        Assert.Contains(result.Teachers, t => t.Code == "UNASSIGNED-TT" && t.IsPlaceholder);
        Assert.DoesNotContain(result.Lessons, l => l.TeacherCode == "UNASSIGNED-TT"); // DATA-1
        Assert.Null(Assert.Single(result.Assignments, a => a.SectionText == "Grade 9 - Rose").SectionExternalId);
        Assert.Contains(result.Subjects, s => s.Name == "Dance" && s.Kind == SubjectKind.Unknown);
        Assert.Contains(result.Subjects, s => s.Name == "Mathematics" && s.Kind == SubjectKind.Subject);

        // Lessons carry the formally assigned teacher (T2) and a cross-section teacher is never double-booked (H1).
        Assert.All(result.Lessons.Where(l => l.SubjectName == "Mathematics"), l => Assert.Equal("T1", l.TeacherCode));
        Assert.Equal(8, result.Lessons.Where(l => l.TeacherCode == "T1").Select(l => (l.Day, l.Period)).Distinct().Count());
    }

    [Fact]
    public void Infeasible_run_stores_conflicts_but_no_lessons()
    {
        var synth = SmallSchool.Overloaded();
        var dataset = synth.Dataset();
        var run = TimetableEngine.Run(dataset, new RuleOptions(), Synth.Fast(), solve: true);
        Assert.False(run.Report.Success);

        var result = RunResultMapper.Map(Guid.NewGuid(), dataset, run, new RuleOptions(), DateTimeOffset.UtcNow);

        Assert.Empty(result.Lessons);
        Assert.NotEmpty(result.SchedulingConflicts);
        Assert.Equal("INFEASIBLE", result.Outcome);
        Assert.True(result.Success == false);
    }

    [Theory]
    [InlineData(null, 5, null)]
    [InlineData("   ", 5, null)]
    [InlineData("  abc ", 5, "abc")]
    [InlineData("abcdefgh", 5, "abcde")]
    public void Clip_trims_and_bounds(string? input, int max, string? expected) => Assert.Equal(expected, RunResultMapper.Clip(input, max));
}
