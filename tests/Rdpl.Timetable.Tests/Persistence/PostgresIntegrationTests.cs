using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Tests.TestSupport;

namespace Rdpl.Timetable.Tests.Persistence;

/// <summary>
/// Runs against a real PostgreSQL server (see <see cref="PostgresFactAttribute"/>). Each test creates its own
/// throw-away database, applies the schema with EnsureCreated (migrations arrive in a later stage) and drops it.
/// </summary>
public sealed class PostgresIntegrationTests
{
    [PostgresFact]
    public async Task Schema_is_created_and_a_complete_run_round_trips()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        var runId = Guid.NewGuid();
        await using (var db = scope.NewContext())
        {
            var (section, subject, teacher) = AddMasters(db);
            db.TimetableRuns.Add(NewRun(runId));
            db.TeachingAssignments.Add(new TeachingAssignmentEntity
            {
                RunId = runId, Section = section, Subject = subject, Teacher = teacher,
                SectionText = "Grade 10 - Lily", SubjectText = "Mathematics", TeacherCode = "RVS115", TeacherName = "Ashish Thakur",
                WorkloadPerWeek = 0, SourceFile = "TEACHER_CLASS_ALLOCATION.DUMMY.xlsx", SourceSheet = "Section Assignments", SourceRow = 2,
                DataOrigin = "DUMMY (derived from TEACHER_CLASS_ASSIGNMENTS.md)", IsDummy = true,
            });
            db.TimetableEntries.Add(Entry(runId, section, subject, teacher, TimetableDay.Monday, 1));
            db.DataConflicts.Add(new DataConflictEntity { RunId = runId, Type = "DUMMY_DATA_IN_USE", Severity = "warning", Message = "Assignments workbook is DUMMY data." });
            db.SchedulingConflicts.Add(new SchedulingConflictEntity
            {
                RunId = runId, Code = "L1_DAY_CAPACITY", RuleId = "L1", Message = "needs 7 days", ProvesInfeasibility = true,
                Sections = new List<string> { "Grade 11 - Lily", "Grade 11 - Lavender" },
            });
            await db.SaveChangesAsync();
        }

        await using (var db = scope.NewContext())
        {
            var run = await db.TimetableRuns.AsNoTracking()
                .Include(r => r.TimetableEntries).ThenInclude(e => e.Teacher)
                .Include(r => r.SchedulingConflicts)
                .Include(r => r.TeachingAssignments)
                .SingleAsync(r => r.Id == runId);
            Assert.True(run.UsesDummyData);
            Assert.Equal("DUMMY_ASSIGNMENTS", run.DataSourceKind);
            Assert.True(run.Version != 0); // xmin populated by PostgreSQL
            var entry = Assert.Single(run.TimetableEntries);
            Assert.Equal(TimetableDay.Monday, entry.Day);
            Assert.Equal(new TimeOnly(7, 45), entry.StartTime);
            Assert.Equal("Ashish Thakur", entry.Teacher!.Name);
            Assert.Equal(new[] { "Grade 11 - Lily", "Grade 11 - Lavender" }, Assert.Single(run.SchedulingConflicts).Sections);
            Assert.True(Assert.Single(run.TeachingAssignments).IsDummy);
        }

        // Stored representation matches the JSON report (upper-case day, jsonb report).
        await using var connection = new NpgsqlConnection(scope.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT day, (SELECT report_json->>'outcome' FROM timetable.timetable_runs) FROM timetable.timetable_entries", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("MONDAY", reader.GetString(0));
        Assert.Equal("TIMETABLE_GENERATED", reader.GetString(1));
    }

    [PostgresFact]
    public async Task Database_rejects_two_lessons_in_one_section_slot_H3()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        await using var db = scope.NewContext();
        var (section, subject, _) = AddMasters(db);
        var runId = Guid.NewGuid();
        db.TimetableRuns.Add(NewRun(runId));
        db.TimetableEntries.Add(Entry(runId, section, subject, null, TimetableDay.Tuesday, 3));
        db.TimetableEntries.Add(Entry(runId, section, subject, null, TimetableDay.Tuesday, 3));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task Database_rejects_teacher_double_booking_H1_but_allows_unbooked_activities()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        var runId = Guid.NewGuid();
        await using (var db = scope.NewContext())
        {
            var (section, subject, teacher) = AddMasters(db);
            var other = new SectionEntity { ExternalId = "class-10-lavender", DisplayName = "Grade 10 - Lavender", Grade = "10", SectionName = "Lavender", CurriculumKey = "Class 10", Capacity = 40 };
            db.TimetableRuns.Add(NewRun(runId));

            // T6/AC8: activities carry no teacher, so two sections may run them in the same slot.
            db.TimetableEntries.Add(Entry(runId, section, subject, null, TimetableDay.Friday, 9, "ACTIVITY"));
            db.TimetableEntries.Add(Entry(runId, other, subject, null, TimetableDay.Friday, 9, "ACTIVITY"));
            await db.SaveChangesAsync();

            db.TimetableEntries.Add(Entry(runId, section, subject, teacher, TimetableDay.Friday, 10));
            db.TimetableEntries.Add(Entry(runId, other, subject, teacher, TimetableDay.Friday, 10));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var pg = Assert.IsType<PostgresException>(ex.InnerException);
            Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
            Assert.Equal("ux_timetable_entries_teacher_slot", pg.ConstraintName);
        }
    }

    [PostgresFact]
    public async Task Check_constraints_reject_invalid_period_and_run_bounds()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        await using var db = scope.NewContext();
        var (section, subject, _) = AddMasters(db);
        var runId = Guid.NewGuid();
        db.TimetableRuns.Add(NewRun(runId));
        db.TimetableEntries.Add(Entry(runId, section, subject, null, TimetableDay.Monday, 13));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, pg.SqlState);
        Assert.Equal("ck_timetable_entries_period", pg.ConstraintName);

        db.ChangeTracker.Clear();
        var bad = NewRun(Guid.NewGuid());
        bad.TimeLimitSeconds = 0;
        db.TimetableRuns.Add(bad);
        ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("ck_timetable_runs_time_limit", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }

    [PostgresFact]
    public async Task Deleting_a_run_cascades_to_its_rows_but_keeps_master_data()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        var runId = Guid.NewGuid();
        await using (var db = scope.NewContext())
        {
            var (section, subject, teacher) = AddMasters(db);
            db.TimetableRuns.Add(NewRun(runId));
            db.TimetableEntries.Add(Entry(runId, section, subject, teacher, TimetableDay.Monday, 1));
            db.DataConflicts.Add(new DataConflictEntity { RunId = runId, Type = "ZERO_WORKLOAD", Severity = "warning", Message = "x" });
            await db.SaveChangesAsync();
        }

        await using (var db = scope.NewContext())
        {
            Assert.Equal(1, await db.TimetableRuns.Where(r => r.Id == runId).ExecuteDeleteAsync());
            Assert.Equal(0, await db.TimetableEntries.CountAsync());
            Assert.Equal(0, await db.DataConflicts.CountAsync());
            Assert.Equal(1, await db.Sections.CountAsync());
            Assert.Equal(1, await db.Teachers.CountAsync());
        }
    }

    [PostgresFact]
    public async Task Concurrent_run_updates_are_detected_through_xmin()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        var runId = Guid.NewGuid();
        await using (var db = scope.NewContext())
        {
            db.TimetableRuns.Add(NewRun(runId));
            await db.SaveChangesAsync();
        }

        await using var first = scope.NewContext();
        await using var second = scope.NewContext();
        var a = await first.TimetableRuns.SingleAsync(r => r.Id == runId);
        var b = await second.TimetableRuns.SingleAsync(r => r.Id == runId);
        a.State = RunState.Completed;
        await first.SaveChangesAsync();
        b.State = RunState.Failed;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task Audit_timestamps_are_utc_and_created_is_immutable()
    {
        await using var scope = await PostgresTestDatabase.CreateAsync();
        int id;
        DateTimeOffset created;
        await using (var db = scope.NewContext())
        {
            var teacher = new TeacherEntity { Code = "RVS190", Name = "Soumya Nair" };
            db.Teachers.Add(teacher);
            await db.SaveChangesAsync();
            id = teacher.Id;
            created = teacher.CreatedUtc;
            Assert.Equal(TimeSpan.Zero, created.Offset);
        }

        await using (var db = scope.NewContext())
        {
            var teacher = await db.Teachers.SingleAsync(t => t.Id == id);
            teacher.Name = "Soumya Nair (renamed)";
            teacher.CreatedUtc = DateTimeOffset.UnixEpoch; // must be ignored
            await db.SaveChangesAsync();
        }

        await using (var db = scope.NewContext())
        {
            var teacher = await db.Teachers.AsNoTracking().SingleAsync(t => t.Id == id);
            Assert.Equal(created.UtcTicks / 10, teacher.CreatedUtc.UtcTicks / 10); // PostgreSQL stores microseconds
            Assert.True(teacher.UpdatedUtc >= teacher.CreatedUtc);
        }
    }

    private static (SectionEntity Section, SubjectEntity Subject, TeacherEntity Teacher) AddMasters(AppDbContext db)
    {
        var section = new SectionEntity { ExternalId = "class-10-lily", DisplayName = "Grade 10 - Lily", Grade = "10", SectionName = "Lily", CurriculumKey = "Class 10", Capacity = 40 };
        var subject = new SubjectEntity { Name = "Mathematics", NormalizedName = SubjectEntity.Normalize("Mathematics"), Kind = SubjectKind.Subject };
        var teacher = new TeacherEntity { Code = "RVS115", Name = "Ashish Thakur" };
        db.AddRange(section, subject, teacher);
        return (section, subject, teacher);
    }

    private static TimetableRunEntity NewRun(Guid id) => new()
    {
        Id = id,
        Kind = RunKind.Generation,
        State = RunState.Running,
        Profile = "pragmatic",
        CreatedUtc = DateTimeOffset.UtcNow,
        DataSourceKind = "DUMMY_ASSIGNMENTS",
        UsesDummyData = true,
        DataSourceWarning = "Assignments workbook is DUMMY data.",
        TimeLimitSeconds = 300,
        Workers = 4,
        RandomSeed = 42,
        ReportJson = "{\"outcome\":\"TIMETABLE_GENERATED\",\"success\":true}",
    };

    private static TimetableEntryEntity Entry(Guid runId, SectionEntity section, SubjectEntity subject, TeacherEntity? teacher, TimetableDay day, short period, string type = "SUBJECT") => new()
    {
        RunId = runId,
        Section = section,
        Subject = subject,
        Teacher = teacher,
        Day = day,
        Period = period,
        Slot = "1A",
        StartTime = new TimeOnly(7, 45),
        EndTime = new TimeOnly(8, 25),
        LessonType = type,
    };
}
