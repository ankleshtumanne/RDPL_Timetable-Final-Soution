using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Reporting;

namespace Rdpl.Timetable.Tests.TestSupport;

/// <summary>
/// Proves the section / teacher / conflict services (which read repository rows) return exactly what the original
/// endpoints returned (which read the report). Compared as serialized JSON text, so key order and nulls count too.
/// </summary>
internal static class ReportParity
{
    public static async Task AssertReadEndpointsMatchReportAsync(ServiceHost host)
    {
        var reportJson = await host.RequestAsync(s => s.GetLatestResultJsonAsync(CancellationToken.None));
        Assert.NotNull(reportJson);
        var report = JsonNode.Parse(reportJson!)!.AsObject();

        await using var scope = host.Provider.CreateAsyncScope();
        var sections = scope.ServiceProvider.GetRequiredService<ISectionService>();
        var teachers = scope.ServiceProvider.GetRequiredService<ITeacherService>();
        var conflicts = scope.ServiceProvider.GetRequiredService<IConflictService>();

        var all = Node(await sections.GetAllAsync(CancellationToken.None));
        AssertSame(report["success"], all["success"], "success");
        AssertSame(report["outcome"], all["outcome"], "outcome");
        AssertSame(report["dataSource"]!["kind"], all["dataSource"], "dataSource");
        AssertSame(report["sectionTimetables"], all["sectionTimetables"], "sectionTimetables");

        foreach (var (name, week) in report["sectionTimetables"]?.AsObject() ?? new JsonObject())
        {
            var one = Node(await sections.GetAsync(name, CancellationToken.None));
            AssertSame(week, one["timetable"], $"sections/{name}");
            Assert.Equal(name, one["section"]!.GetValue<string>());
        }

        var teacherView = Node(await teachers.GetAllAsync(CancellationToken.None));
        AssertSame(report["teacherTimetables"], teacherView["teacherTimetables"], "teacherTimetables");

        var conflictView = Node(await conflicts.GetLatestAsync(CancellationToken.None));
        AssertSame(report["dataConflicts"], conflictView["dataConflicts"], "dataConflicts");
        AssertSame(report["schedulingConflicts"], conflictView["schedulingConflicts"], "schedulingConflicts");
        AssertSame(report["summary"]!["dataConflictCounts"], conflictView["dataConflictCounts"], "dataConflictCounts");
        AssertSame(report["diagnostic"]?["relaxedHardRuleViolations"], conflictView["diagnostic"], "diagnostic");
    }

    public static JsonObject Node<T>(T dto) => JsonNode.Parse(JsonSerializer.Serialize(dto, TimetableReport.JsonOptions))!.AsObject();

    private static void AssertSame(JsonNode? expected, JsonNode? actual, string what) =>
        Assert.True(
            string.Equals(expected?.ToJsonString(TimetableReport.JsonOptions), actual?.ToJsonString(TimetableReport.JsonOptions), StringComparison.Ordinal),
            $"{what} differs from the report.\nexpected: {Short(expected)}\nactual:   {Short(actual)}");

    private static string Short(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "null";
        return text.Length > 300 ? text[..300] + "…" : text;
    }
}
