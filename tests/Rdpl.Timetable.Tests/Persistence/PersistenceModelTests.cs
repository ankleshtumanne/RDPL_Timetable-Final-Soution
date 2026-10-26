using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Tests.Persistence;

/// <summary>Checks the EF Core model and generated DDL. No database connection is opened.</summary>
public sealed class PersistenceModelTests
{
    private static AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=model_only;Username=model_only")
        .Options);

    [Fact]
    public void All_tables_live_in_the_timetable_schema_with_snake_case_names()
    {
        using var db = NewContext();
        var tables = db.Model.GetEntityTypes().Select(e => (e.GetSchema(), e.GetTableName())).OrderBy(t => t.Item2).ToList();
        Assert.Equal(
            new[] { "data_conflicts", "scheduling_conflicts", "sections", "subjects", "teachers", "teaching_assignments", "timetable_entries", "timetable_runs" },
            tables.Select(t => t.Item2!));
        Assert.All(tables, t => Assert.Equal(AppDbContext.Schema, t.Item1));
    }

    [Fact]
    public void Columns_are_snake_case_and_the_version_maps_to_xmin()
    {
        using var db = NewContext();
        var run = db.Model.FindEntityType(typeof(TimetableRunEntity))!;
        var table = StoreObjectIdentifier.Table("timetable_runs", AppDbContext.Schema);
        Assert.Equal("data_source_kind", run.FindProperty(nameof(TimetableRunEntity.DataSourceKind))!.GetColumnName(table));
        Assert.Equal("hard_constraints_satisfied", run.FindProperty(nameof(TimetableRunEntity.HardConstraintsSatisfied))!.GetColumnName(table));
        Assert.Equal("xmin", run.FindProperty(nameof(TimetableRunEntity.Version))!.GetColumnName(table));
        Assert.Equal("json", run.FindProperty(nameof(TimetableRunEntity.ReportJson))!.GetColumnType()); // json keeps the report text verbatim

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var id = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            Assert.All(entity.GetProperties(), p => Assert.Equal(p.GetColumnName(id)!.ToLowerInvariant(), p.GetColumnName(id)));
        }
    }

    [Fact]
    public void Database_enforces_H3_and_H1_with_unique_indexes()
    {
        using var db = NewContext();
        var entry = db.Model.FindEntityType(typeof(TimetableEntryEntity))!;
        var sectionSlot = Assert.Single(entry.GetIndexes(), i => i.GetDatabaseName() == "ux_timetable_entries_section_slot");
        Assert.True(sectionSlot.IsUnique);
        Assert.Null(sectionSlot.GetFilter());

        var teacherSlot = Assert.Single(entry.GetIndexes(), i => i.GetDatabaseName() == "ux_timetable_entries_teacher_slot");
        Assert.True(teacherSlot.IsUnique);
        Assert.Equal("teacher_id IS NOT NULL", teacherSlot.GetFilter());
        Assert.Equal(new[] { "RunId", "TeacherId", "Day", "Period" }, teacherSlot.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Run_children_cascade_but_master_rows_are_protected()
    {
        using var db = NewContext();
        foreach (var child in new[] { typeof(TeachingAssignmentEntity), typeof(TimetableEntryEntity), typeof(DataConflictEntity), typeof(SchedulingConflictEntity) })
        {
            var toRun = Assert.Single(db.Model.FindEntityType(child)!.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(TimetableRunEntity));
            Assert.Equal(DeleteBehavior.Cascade, toRun.DeleteBehavior);
        }

        var masters = new[] { typeof(SectionEntity), typeof(SubjectEntity), typeof(TeacherEntity) };
        var masterFks = db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).Where(fk => masters.Contains(fk.PrincipalEntityType.ClrType)).ToList();
        Assert.NotEmpty(masterFks);
        Assert.All(masterFks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void Conflict_tables_keep_free_text_instead_of_foreign_keys()
    {
        // A data conflict often describes a row whose section or teacher does not exist, so it must not need a FK.
        using var db = NewContext();
        var fks = db.Model.FindEntityType(typeof(DataConflictEntity))!.GetForeignKeys().ToList();
        Assert.Equal(typeof(TimetableRunEntity), Assert.Single(fks).PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Generated_ddl_contains_tables_constraints_and_postgres_types()
    {
        using var db = NewContext();
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE SCHEMA timetable;", sql); // EF wraps it in IF NOT EXISTS (pg_namespace) for idempotency
        Assert.Contains("CREATE TABLE timetable.timetable_entries", sql);
        Assert.Contains("instructors text[] NOT NULL", sql);
        Assert.Contains("report_json json", sql);
        Assert.DoesNotContain("jsonb", sql);
        Assert.Contains("start_time time without time zone NOT NULL", sql);
        Assert.Contains("created_utc timestamp with time zone NOT NULL", sql);
        Assert.Contains("CONSTRAINT ck_timetable_entries_period CHECK (period BETWEEN 1 AND 12)", sql);
        Assert.Contains("CREATE UNIQUE INDEX ux_timetable_entries_teacher_slot ON timetable.timetable_entries (run_id, teacher_id, day, period) WHERE teacher_id IS NOT NULL;", sql);
        Assert.Contains("GENERATED ALWAYS AS IDENTITY", sql);
        Assert.DoesNotContain("xmin", sql); // system column: must not be created
        Assert.DoesNotContain(" version ", sql);
    }

    [Fact]
    public void Every_identifier_fits_the_postgres_63_character_limit()
    {
        // PostgreSQL silently truncates longer names, which makes later migrations unreliable.
        using var db = NewContext();
        var model = db.GetService<IDesignTimeModel>().Model; // includes check constraints
        var names = new List<string>();
        foreach (var entity in model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            names.Add(entity.GetTableName()!);
            names.AddRange(entity.GetProperties().Select(p => p.GetColumnName(table)!));
            names.AddRange(entity.GetKeys().Select(k => k.GetName()!));
            names.AddRange(entity.GetForeignKeys().Select(fk => fk.GetConstraintName()!));
            names.AddRange(entity.GetIndexes().Select(i => i.GetDatabaseName()!));
            names.AddRange(entity.GetCheckConstraints().Select(c => c.Name!));
        }

        Assert.All(names, n => Assert.True(n.Length <= 63, $"'{n}' is {n.Length} characters"));
    }

    [Theory]
    [InlineData("ExternalId", "external_id")]
    [InlineData("IsPrePrimary", "is_pre_primary")]
    [InlineData("RunId", "run_id")]
    [InlineData("WallTimeMs", "wall_time_ms")]
    [InlineData("ReportJSON", "report_json")]
    [InlineData("Id", "id")]
    public void Snake_case_conversion(string input, string expected) => Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));

    [Fact]
    public void Subject_names_normalize_for_uniqueness()
    {
        Assert.Equal("GAMES / LIBRARY", SubjectEntity.Normalize("  Games  /   Library "));
        Assert.Equal(SubjectEntity.Normalize("english language"), SubjectEntity.Normalize("English Language"));
    }
}
