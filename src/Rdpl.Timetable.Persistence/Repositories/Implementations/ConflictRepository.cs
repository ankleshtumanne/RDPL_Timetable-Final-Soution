using Microsoft.EntityFrameworkCore;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Implementations;

/// <summary>EF Core / PostgreSQL implementation of <see cref="IConflictRepository"/>. Read-only, no tracking.</summary>
public sealed class ConflictRepository : IConflictRepository
{
    private readonly AppDbContext _db;

    public ConflictRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DataConflictData>> GetDataConflictsAsync(Guid runId, CancellationToken cancellationToken) =>
        await _db.DataConflicts.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Id)
            .Select(c => new DataConflictData(c.Type, c.Severity, c.Message, c.SectionText, c.SubjectText, c.TeacherCode, c.Source, c.RuleId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SchedulingConflictData>> GetSchedulingConflictsAsync(Guid runId, CancellationToken cancellationToken) =>
        await _db.SchedulingConflicts.AsNoTracking()
            .Where(c => c.RunId == runId)
            .OrderBy(c => c.Id)
            .Select(c => new SchedulingConflictData(c.Code, c.RuleId, c.Message, c.ProvesInfeasibility, c.Sections, c.TeacherCode))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
