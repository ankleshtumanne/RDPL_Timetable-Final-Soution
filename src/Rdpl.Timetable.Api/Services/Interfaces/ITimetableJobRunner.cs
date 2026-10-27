using Rdpl.Timetable.Api.DTOs;

namespace Rdpl.Timetable.Api.Services.Interfaces;

public interface ITimetableJobRunner
{
    bool IsBusy { get; }
    bool TryReserve();
    void CancelReservation();
    void Start(Guid runId, RunSettings settings);
}
