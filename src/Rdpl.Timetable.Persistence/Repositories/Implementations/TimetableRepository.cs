using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>
/// EF Core + PostgreSQL implementation. Scoped (one <see cref="AppDbContext"/> per request / background job).
/// <see cref="CompleteRunAsync"/> runs as ONE transaction inside the execution (retry) strategy, so a run is stored
/// completely or not at all. Master tables are upserted set-based with <c>INSERT … ON CONFLICT</c> (safe when two runs
/// finish at the same time), and every SQL value is passed as a parameter.
/// </summary>
public sealed class TimetableRepository : ITimetableRepository
{
    private readonly AppDbContext _db;

    public TimetableRepository(AppDbContext db)
    {
        _db = db;
    }

    public bool IsDurable => true;

    public async Task AddRunAsync(NewRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        _db.TimetableRuns.Add(new TimetableRunEntity
        {
            Id = run.Id,
            Kind = run.Kind,
            State = RunState.Running,
            Profile = run.Profile,
            CreatedUtc = run.CreatedUtc,
            DataSourceKind = "PENDING",
            TimeLimitSeconds = run.TimeLimitSeconds,
            Workers = run.Workers,
            RandomSeed = run.RandomSeed,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
    }

    public async Task CompleteRunAsync(RunResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            async ct =>
            {
                // Retry-safe: each attempt starts from a clean change tracker inside a new transaction.
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

                var run = await _db.TimetableRuns.SingleOrDefaultAsync(r => r.Id == result.RunId, ct).ConfigureAwait(false)
                          ?? throw new InvalidOperationException($"Run {result.RunId} does not exist.");
                if (run.State != RunState.Running)
                {
                    throw new InvalidOperationException($"Run {result.RunId} is {run.State}; only running runs can be completed.");
                }

                var sectionIds = await UpsertSectionsAsync(result.Sections, ct).ConfigureAwait(false);
                var teacherIds = await UpsertTeachersAsync(result.Teachers, ct).ConfigureAwait(false);
                var subjectIds = await UpsertSubjectsAsync(result.Subjects, ct).ConfigureAwait(false);

                ApplyRunFields(run, result);
                AddChildren(result, sectionIds, teacherIds, subjectIds);

                _db.ChangeTracker.DetectChanges();
                _db.ChangeTracker.AutoDetectChangesEnabled = false;
                try
                {
                    await _db.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    _db.ChangeTracker.AutoDetectChangesEnabled = true;
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        _db.ChangeTracker.Clear();
    }

    public async Task<bool> FailRunAsync(Guid runId, string error, DateTimeOffset completedUtc, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var updated = await _db.TimetableRuns
            .Where(r => r.Id == runId && r.State == RunState.Running)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.State, RunState.Failed)
                      .SetProperty(r => r.Error, error)
                      .SetProperty(r => r.CompletedUtc, completedUtc),
                cancellationToken).ConfigureAwait(false);
        return updated > 0;
    }

    public Task<RunRecord?> GetRunAsync(Guid runId, CancellationToken cancellationToken) =>
        Project(_db.TimetableRuns.Where(r => r.Id == runId)).SingleOrDefaultAsync(cancellationToken);

    public Task<RunRecord?> GetLatestCompletedRunAsync(RunKind kind, CancellationToken cancellationToken) =>
        Project(_db.TimetableRuns
                .Where(r => r.Kind == kind && r.State == RunState.Completed)
                .OrderByDescending(r => r.CompletedUtc)
                .ThenByDescending(r => r.CreatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetReportJsonAsync(Guid runId, CancellationToken cancellationToken) =>
        _db.TimetableRuns.AsNoTracking()
            .Where(r => r.Id == runId && r.State == RunState.Completed)
            .Select(r => r.ReportJson)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RunRecord>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);
        return await Project(_db.TimetableRuns.OrderByDescending(r => r.CreatedUtc).ThenBy(r => r.Id).Skip(skip).Take(take))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<int> CountRunsAsync(CancellationToken cancellationToken) => _db.TimetableRuns.CountAsync(cancellationToken);

    public Task<int> FailAbandonedRunsAsync(string error, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var now = DateTimeOffset.UtcNow;
        return _db.TimetableRuns
            .Where(r => r.State == RunState.Running)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.State, RunState.Failed)
                      .SetProperty(r => r.Error, error)
                      .SetProperty(r => r.CompletedUtc, now),
                cancellationToken);
    }

    private static IQueryable<RunRecord> Project(IQueryable<TimetableRunEntity> query) => query.AsNoTracking().Select(r => new RunRecord
    {
        Id = r.Id,
        Kind = r.Kind,
        State = r.State,
        Profile = r.Profile,
        CreatedUtc = r.CreatedUtc,
        CompletedUtc = r.CompletedUtc,
        Outcome = r.Outcome,
        Success = r.Success,
        DataSourceKind = r.DataSourceKind,
        UsesDummyData = r.UsesDummyData,
        SolverStatus = r.SolverStatus,
        WallTimeMs = r.WallTimeMs,
        TimeLimitSeconds = r.TimeLimitSeconds,
        HardConstraintsSatisfied = r.HardConstraintsSatisfied,
        SectionsScheduled = r.SectionsScheduled,
        TotalSlotsScheduled = r.TotalSlotsScheduled,
        Error = r.Error,
        SolverJson = r.SolverJson,
        SummaryJson = r.SummaryJson,
        DiagnosticJson = r.DiagnosticJson,
        LessonCount = r.TimetableEntries.Count(),
        DataConflictCount = r.DataConflicts.Count(),
        SchedulingConflictCount = r.SchedulingConflicts.Count(),
    });

    private static void ApplyRunFields(TimetableRunEntity run, RunResult r)
    {
        run.State = r.State;
        run.CompletedUtc = r.CompletedUtc;
        run.Outcome = r.Outcome;
        run.Success = r.Success;
        run.DataSourceKind = r.DataSourceKind;
        run.UsesDummyData = r.UsesDummyData;
        run.DataSourceWarning = r.DataSourceWarning;
        run.AssignmentsWorkbook = r.AssignmentsWorkbook;
        run.SolverStatus = r.SolverStatus;
        run.WallTimeMs = r.WallTimeMs;
        run.ObjectiveValue = r.ObjectiveValue;
        run.HardConstraintsSatisfied = r.HardConstraintsSatisfied;
        run.HardViolationCount = r.HardViolationCount;
        run.SoftConstraintPenalty = r.SoftConstraintPenalty;
        run.SectionsInScope = r.SectionsInScope;
        run.SectionsScheduled = r.SectionsScheduled;
        run.TotalSlotsScheduled = r.TotalSlotsScheduled;
        run.ReportJson = r.ReportJson;
        run.SolverJson = r.SolverJson;
        run.SummaryJson = r.SummaryJson;
        run.DiagnosticJson = r.DiagnosticJson;
        run.Error = null;
    }

    private void AddChildren(RunResult r, Dictionary<string, int> sections, Dictionary<string, int> teachers, Dictionary<string, int> subjects)
    {
        int? Resolve(Dictionary<string, int> map, string? key) => key is not null && map.TryGetValue(key, out var id) ? id : null;
        int Require(Dictionary<string, int> map, string key, string what) =>
            map.TryGetValue(key, out var id) ? id : throw new InvalidOperationException($"Lesson references unknown {what} '{key}'.");

        _db.TeachingAssignments.AddRange(r.Assignments.Select(a => new TeachingAssignmentEntity
        {
            RunId = r.RunId,
            SectionId = Resolve(sections, a.SectionExternalId),
            SubjectId = Resolve(subjects, a.SubjectName is null ? null : SubjectEntity.Normalize(a.SubjectName)),
            TeacherId = Resolve(teachers, a.TeacherCode),
            SectionText = a.SectionText,
            SubjectText = a.SubjectText,
            TeacherCode = a.TeacherCodeText,
            TeacherName = a.TeacherNameText,
            WorkloadPerWeek = a.WorkloadPerWeek,
            SourceFile = a.SourceFile,
            SourceSheet = a.SourceSheet,
            SourceRow = a.SourceRow,
            DataOrigin = a.DataOrigin,
            IsDummy = a.IsDummy,
        }));

        _db.TimetableEntries.AddRange(r.Lessons.Select(l => new TimetableEntryEntity
        {
            RunId = r.RunId,
            SectionId = Require(sections, l.SectionExternalId, "section"),
            SubjectId = Require(subjects, SubjectEntity.Normalize(l.SubjectName), "subject"),
            TeacherId = l.TeacherCode is null ? null : Require(teachers, l.TeacherCode, "teacher"),
            TeacherName = l.TeacherName,
            Day = l.Day,
            Period = checked((short)l.Period),
            Slot = l.Slot,
            StartTime = l.StartTime,
            EndTime = l.EndTime,
            LessonType = l.LessonType,
            Instructors = l.Instructors.ToList(),
            Note = l.Note,
        }));

        _db.DataConflicts.AddRange(r.DataConflicts.Select(c => new DataConflictEntity
        {
            RunId = r.RunId,
            Type = c.Type,
            Severity = c.Severity,
            Message = c.Message,
            SectionText = c.Section,
            SubjectText = c.Subject,
            TeacherCode = c.TeacherCode,
            Source = c.Source,
            RuleId = c.RuleId,
        }));

        _db.SchedulingConflicts.AddRange(r.SchedulingConflicts.Select(c => new SchedulingConflictEntity
        {
            RunId = r.RunId,
            Code = c.Code,
            RuleId = c.RuleId,
            Message = c.Message,
            ProvesInfeasibility = c.ProvesInfeasibility,
            Sections = c.Sections.ToList(),
            TeacherCode = c.TeacherCode,
        }));
    }

    private async Task<Dictionary<string, int>> UpsertSectionsAsync(IReadOnlyList<SectionData> input, CancellationToken ct)
    {
        var rows = input.GroupBy(s => s.ExternalId, StringComparer.Ordinal).Select(g => g.First()).ToList();
        if (rows.Count > 0)
        {
            const string sql = """
                INSERT INTO timetable.sections AS t (external_id, display_name, grade, section_name, curriculum_key, capacity, is_pre_primary, created_utc, updated_utc)
                SELECT u.external_id, u.display_name, u.grade, u.section_name, u.curriculum_key, u.capacity, u.is_pre_primary, now(), now()
                FROM unnest(@ids, @names, @grades, @sections, @keys, @capacities, @pre) AS u(external_id, display_name, grade, section_name, curriculum_key, capacity, is_pre_primary)
                ON CONFLICT (external_id) DO UPDATE
                SET display_name = EXCLUDED.display_name, grade = EXCLUDED.grade, section_name = EXCLUDED.section_name,
                    curriculum_key = EXCLUDED.curriculum_key, capacity = EXCLUDED.capacity, is_pre_primary = EXCLUDED.is_pre_primary, updated_utc = now()
                WHERE (t.display_name, t.grade, t.section_name, t.curriculum_key, t.capacity, t.is_pre_primary)
                      IS DISTINCT FROM (EXCLUDED.display_name, EXCLUDED.grade, EXCLUDED.section_name, EXCLUDED.curriculum_key, EXCLUDED.capacity, EXCLUDED.is_pre_primary)
                """;
            await _db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    new NpgsqlParameter("ids", rows.Select(r => r.ExternalId).ToArray()),
                    new NpgsqlParameter("names", rows.Select(r => r.DisplayName).ToArray()),
                    new NpgsqlParameter("grades", rows.Select(r => r.Grade).ToArray()),
                    new NpgsqlParameter("sections", rows.Select(r => r.SectionName).ToArray()),
                    new NpgsqlParameter("keys", rows.Select(r => r.CurriculumKey).ToArray()),
                    new NpgsqlParameter("capacities", rows.Select(r => r.Capacity).ToArray()),
                    new NpgsqlParameter("pre", rows.Select(r => r.IsPrePrimary).ToArray()),
                },
                ct).ConfigureAwait(false);
        }

        var keys = rows.Select(r => r.ExternalId).ToList();
        return await _db.Sections.AsNoTracking().Where(s => keys.Contains(s.ExternalId))
            .ToDictionaryAsync(s => s.ExternalId, s => s.Id, StringComparer.Ordinal, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, int>> UpsertTeachersAsync(IReadOnlyList<TeacherData> input, CancellationToken ct)
    {
        var rows = input.GroupBy(t => t.Code, StringComparer.Ordinal).Select(g => g.First()).ToList();
        if (rows.Count > 0)
        {
            const string sql = """
                INSERT INTO timetable.teachers AS t (code, name, is_placeholder, created_utc, updated_utc)
                SELECT u.code, u.name, u.is_placeholder, now(), now()
                FROM unnest(@codes, @names, @placeholders) AS u(code, name, is_placeholder)
                ON CONFLICT (code) DO UPDATE
                SET name = EXCLUDED.name, is_placeholder = EXCLUDED.is_placeholder, updated_utc = now()
                WHERE (t.name, t.is_placeholder) IS DISTINCT FROM (EXCLUDED.name, EXCLUDED.is_placeholder)
                """;
            await _db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    new NpgsqlParameter("codes", rows.Select(r => r.Code).ToArray()),
                    new NpgsqlParameter("names", rows.Select(r => r.Name).ToArray()),
                    new NpgsqlParameter("placeholders", rows.Select(r => r.IsPlaceholder).ToArray()),
                },
                ct).ConfigureAwait(false);
        }

        var keys = rows.Select(r => r.Code).ToList();
        return await _db.Teachers.AsNoTracking().Where(t => keys.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, t => t.Id, StringComparer.Ordinal, ct).ConfigureAwait(false);
    }

    private async Task<Dictionary<string, int>> UpsertSubjectsAsync(IReadOnlyList<SubjectData> input, CancellationToken ct)
    {
        // One row per normalized name; a known kind wins over Unknown.
        var rows = input
            .GroupBy(s => SubjectEntity.Normalize(s.Name), StringComparer.Ordinal)
            .Select(g => (Normalized: g.Key, Name: g.First().Name.Trim(), Kind: g.Select(x => x.Kind).OrderBy(k => k == SubjectKind.Unknown).First()))
            .ToList();
        if (rows.Count > 0)
        {
            const string sql = """
                INSERT INTO timetable.subjects AS t (name, normalized_name, kind, created_utc, updated_utc)
                SELECT u.name, u.normalized_name, u.kind, now(), now()
                FROM unnest(@names, @normalized, @kinds) AS u(name, normalized_name, kind)
                ON CONFLICT (normalized_name) DO UPDATE
                SET kind = EXCLUDED.kind, updated_utc = now()
                WHERE EXCLUDED.kind <> 'Unknown' AND t.kind IS DISTINCT FROM EXCLUDED.kind
                """;
            await _db.Database.ExecuteSqlRawAsync(
                sql,
                new object[]
                {
                    new NpgsqlParameter("names", rows.Select(r => r.Name).ToArray()),
                    new NpgsqlParameter("normalized", rows.Select(r => r.Normalized).ToArray()),
                    new NpgsqlParameter("kinds", rows.Select(r => r.Kind.ToString()).ToArray()),
                },
                ct).ConfigureAwait(false);
        }

        var keys = rows.Select(r => r.Normalized).ToList();
        return await _db.Subjects.AsNoTracking().Where(s => keys.Contains(s.NormalizedName))
            .ToDictionaryAsync(s => s.NormalizedName, s => s.Id, StringComparer.Ordinal, ct).ConfigureAwait(false);
    }
}
