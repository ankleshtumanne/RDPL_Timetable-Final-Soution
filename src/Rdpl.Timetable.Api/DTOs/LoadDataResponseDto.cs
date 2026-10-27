using Rdpl.Timetable.Reporting;
namespace Rdpl.Timetable.Api.DTOs;
public sealed record LoadDataResponseDto(
    string RunId,
    string Storage,
    string Outcome,
    DataSourceInfo DataSource,
    RulesAppliedInfo RulesApplied,
    SummaryInfo Summary,
    IReadOnlyList<DataConflictDto> DataConflicts,
    IReadOnlyList<SchedulingConflictDto> SchedulingConflicts,
    IReadOnlyList<string> Assumptions)
{
    public static LoadDataResponseDto From(Guid runId, string storage, TimetableReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new LoadDataResponseDto(runId.ToString("N"), storage, report.Outcome, report.DataSource, report.RulesApplied, report.Summary,
            report.DataConflicts, report.SchedulingConflicts, report.Assumptions);
    }
}
