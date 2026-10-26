using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Services.Implementations;
using Rdpl.Timetable.Configuration;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Options;
using Rdpl.Timetable.Persistence.Repositories.Implementations;
using Rdpl.Timetable.Persistence.Repositories.Models;
using Rdpl.Timetable.Tests.TestSupport;

namespace Rdpl.Timetable.Tests.Persistence;

/// <summary>TimetableRepository (EF Core) and the full service → repository → PostgreSQL flow on a real server.</summary>
public sealed class TimetableRepositoryTests
{
    [PostgresFact]
    public async Task Completed_run_stores_masters_input_rows_lessons_conflicts_and_the_verbatim_report()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var (runId, result) = await StoreRunAsync(database, SmallSchool.Build());

        await using var db = database.NewContext();
        Assert.Equal(16, await db.TimetableEntries.CountAsync(e => e.RunId == runId));
        Assert.Equal(result.Assignments.Count, await db.TeachingAssignments.CountAsync(a => a.RunId == runId));
        Assert.Equal(result.DataConflicts.Count, await db.DataConflicts.CountAsync(c => c.RunId == runId));
        Assert.Equal(2, await db.Sections.CountAsync());
        Assert.True(await db.Teachers.AnyAsync(t => t.Code == "UNASSIGNED-TT" && t.IsPlaceholder));
        Assert.Equal(SubjectKind.Unknown, (await db.Subjects.SingleAsync(s => s.NormalizedName == "DANCE")).Kind);

        // Unresolvable input rows are kept with raw text and a null FK.
        var rose = await db.TeachingAssignments.SingleAsync(a => a.SectionText == "Grade 9 - Rose");
        Assert.Null(rose.SectionId);
        Assert.Equal("T9", rose.TeacherCode);

        // T1 teaches both sections and is never booked twice in one slot (also enforced by ux_timetable_entries_teacher_slot).
        var t1 = await db.Teachers.SingleAsync(t => t.Code == "T1");
        var t1Slots = await db.TimetableEntries.Where(e => e.TeacherId == t1.Id).Select(e => new { e.Day, e.Period }).ToListAsync();
        Assert.Equal(8, t1Slots.Count);
        Assert.Equal(8, t1Slots.Distinct().Count());

        var repository = new TimetableRepository(db);
        var record = await repository.GetRunAsync(runId, CancellationToken.None);
        Assert.Equal(RunState.Completed, record!.State);
        Assert.Equal(16, record.LessonCount);
        Assert.Equal(result.ReportJson, await repository.GetReportJsonAsync(runId, CancellationToken.None)); // json keeps the text verbatim
    }

    [PostgresFact]
    public async Task Master_rows_are_upserted_across_runs_and_the_latest_run_wins()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var (first, _) = await StoreRunAsync(database, SmallSchool.Build());
        var (second, _) = await StoreRunAsync(database, SmallSchool.Build());

        await using var db = database.NewContext();
        Assert.Equal(2, await db.Sections.CountAsync());
        Assert.Equal(await db.Teachers.Select(t => t.Code).Distinct().CountAsync(), await db.Teachers.CountAsync());
        Assert.Equal(32, await db.TimetableEntries.CountAsync());

        var repository = new TimetableRepository(db);
        Assert.Equal(second, (await repository.GetLatestCompletedRunAsync(RunKind.Generation, CancellationToken.None))!.Id);
        var page = await repository.ListRunsAsync(0, 1, CancellationToken.None);
        Assert.Equal(second, Assert.Single(page).Id);
        Assert.Equal(first, Assert.Single(await repository.ListRunsAsync(1, 5, CancellationToken.None)).Id);
        Assert.Equal(2, await repository.CountRunsAsync(CancellationToken.None));
    }

    [PostgresFact]
    public async Task Only_running_runs_can_be_completed_or_failed()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var (runId, result) = await StoreRunAsync(database, SmallSchool.Build());

        await using var db = database.NewContext();
        var repository = new TimetableRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CompleteRunAsync(result, CancellationToken.None));
        Assert.False(await repository.FailRunAsync(runId, "late failure", DateTimeOffset.UtcNow, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CompleteRunAsync(new RunResult
        {
            RunId = Guid.NewGuid(), State = RunState.Completed, CompletedUtc = DateTimeOffset.UtcNow, DataSourceKind = "X", ReportJson = "{}",
        }, CancellationToken.None));
        Assert.Equal(16, await db.TimetableEntries.CountAsync()); // nothing duplicated
    }

    [PostgresFact]
    public async Task Failed_and_abandoned_runs_are_recorded_with_safe_messages()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var db = database.NewContext();
        var repository = new TimetableRepository(db);
        var failed = Guid.NewGuid();
        var abandoned = Guid.NewGuid();
        await repository.AddRunAsync(new NewRun(failed, RunKind.Generation, "strict", DateTimeOffset.UtcNow, 60, 2, 1), CancellationToken.None);
        await repository.AddRunAsync(new NewRun(abandoned, RunKind.Generation, "strict", DateTimeOffset.UtcNow, 60, 2, 1), CancellationToken.None);

        Assert.True(await repository.FailRunAsync(failed, "sections.json: invalid JSON.", DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.Equal(1, await repository.FailAbandonedRunsAsync("The server restarted before this run finished.", CancellationToken.None));

        Assert.Equal("sections.json: invalid JSON.", (await repository.GetRunAsync(failed, CancellationToken.None))!.Error);
        var record = await repository.GetRunAsync(abandoned, CancellationToken.None);
        Assert.Equal(RunState.Failed, record!.State);
        Assert.Null(await repository.GetReportJsonAsync(abandoned, CancellationToken.None));
        Assert.Null(await repository.GetLatestCompletedRunAsync(RunKind.Generation, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Service_to_postgresql_end_to_end_with_the_registrations_used_by_the_api()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        await using var host = ServiceHost.Create(() => SmallSchool.Build().Dataset(),
            s => s.AddTimetablePersistence(database.ConnectionString, new PersistenceOptions { Enabled = true }));

        var validation = await host.RequestAsync(s => s.LoadDataAsync(null, CancellationToken.None));
        Assert.Equal("postgresql", validation.Storage);

        var started = await host.RequestAsync(s => s.StartGenerationAsync(new GenerateRequest { TimeLimitSeconds = 20, Workers = 2 }, CancellationToken.None));
        Assert.Equal("postgresql", started.Storage);
        var job = await host.WaitForJobAsync(started.Id);
        Assert.Equal("COMPLETED", job.State);
        Assert.Equal("TIMETABLE_GENERATED", job.Outcome);

        var runs = await host.RequestAsync(s => s.ListRunsAsync(0, 10, CancellationToken.None));
        Assert.Equal(2, runs.Total);
        var generation = Assert.Single(runs.Items, r => r.Kind == "GENERATION");
        Assert.Equal(16, generation.LessonCount);
        Assert.True(generation.HardConstraintsSatisfied);
        await ReportParity.AssertReadEndpointsMatchReportAsync(host);

        await using var db = database.NewContext();
        Assert.Equal(16, await db.TimetableEntries.CountAsync());
        Assert.Equal(2, await db.TimetableRuns.CountAsync(r => r.State == RunState.Completed));
    }

    private static async Task<(Guid RunId, RunResult Result)> StoreRunAsync(PostgresTestDatabase database, Synth synth)
    {
        var dataset = synth.Dataset();
        var run = TimetableEngine.Run(dataset, new RuleOptions(), Synth.Fast(), solve: true);
        Assert.True(run.Report.Success, run.Report.Outcome);
        var runId = Guid.NewGuid();

        // Same order as the service: the run row exists before it completes (ck_timetable_runs_completed).
        await using var db = database.NewContext();
        var repository = new TimetableRepository(db);
        await repository.AddRunAsync(new NewRun(runId, RunKind.Generation, "strict", DateTimeOffset.UtcNow, 20, 2, 7), CancellationToken.None);
        var result = RunResultMapper.Map(runId, dataset, run, new RuleOptions(), DateTimeOffset.UtcNow);
        await repository.CompleteRunAsync(result, CancellationToken.None);
        return (runId, result);
    }
}
