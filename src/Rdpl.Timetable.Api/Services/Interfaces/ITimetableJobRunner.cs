using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>
/// Executes generations in the background, one at a time (CP-SAT already uses every configured worker thread).
/// Singleton. Persists each finished run through a scoped <c>ITimetableRepository</c>.
/// </summary>
public interface ITimetableJobRunner
{
    bool IsBusy { get; }

    /// <summary>Reserves the single solve slot. Returns false when a generation is running.</summary>
    bool TryReserve();

    /// <summary>Gives the slot back when the run could not be started (e.g. the database insert failed).</summary>
    void CancelReservation();

    /// <summary>Starts the reserved run. The slot is released when the run has finished and been stored.</summary>
    void Start(Guid runId, RunSettings settings);
}
