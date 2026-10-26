using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Persistence.Repositories.Interfaces;

/// <summary>Data conflicts and scheduling conflicts of a run, in stored (report) order.</summary>
public interface IConflictRepository
{
    Task<IReadOnlyList<DataConflictData>> GetDataConflictsAsync(Guid runId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SchedulingConflictData>> GetSchedulingConflictsAsync(Guid runId, CancellationToken cancellationToken);
}
