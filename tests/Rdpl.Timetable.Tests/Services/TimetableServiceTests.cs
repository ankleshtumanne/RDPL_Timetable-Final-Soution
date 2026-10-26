using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Data;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Rdpl.Timetable.Tests.Services;

/// <summary>TimetableService + TimetableJobRunner with the in-memory repository (persistence disabled).</summary>
public sealed class TimetableServiceTests
{
    private static ServiceHost NewHost(Func<LoadedDataset>? dataset = null) =>
        ServiceHost.Create(dataset ?? (() => SmallSchool.Build().Dataset()), s => s.AddInMemoryTimetableRepository(10));

    [Fact]
    public async Task Load_data_validates_without_solving_and_records_a_run()
    {
        await using var host = NewHost();
        var response = await host.RequestAsync(s => s.LoadDataAsync(new GenerateRequest { Profile = "strict" }, CancellationToken.None));

        Assert.Equal("DATA_VALIDATION_ONLY", response.Outcome);
        Assert.Equal("memory", response.Storage);
        Assert.Contains(response.DataConflicts, c => c.Type == "UNASSIGNED_TEACHER");

        var repository = host.Provider.GetRequiredService<ITimetableRepository>();
        var run = await repository.GetRunAsync(Guid.ParseExact(response.RunId, "N"), CancellationToken.None);
        Assert.Equal(RunState.Completed, run!.State);
        Assert.Equal(RunKind.DataValidation, run.Kind);
    }
    [Fact]
    public async Task An_unexpected_load_data_error_closes_the_run_as_failed()
    {
        await using var host = NewHost(() => throw new InvalidOperationException("unexpected bug"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RequestAsync(s => s.LoadDataAsync(null, CancellationToken.None)));

        var run = Assert.Single((await host.RequestAsync(s => s.ListRunsAsync(0, 10, CancellationToken.None))).Items);
        Assert.Equal("FAILED", run.State);
        var job = await host.RequestAsync(s => s.GetJobAsync(Guid.ParseExact(run.Id, "N"), CancellationToken.None));
        Assert.StartsWith("Unexpected server error while loading the data.", job!.Error);
        Assert.DoesNotContain("unexpected bug", job.Error);
    }

    [Fact]
    public async Task Generation_runs_in_the_background_and_the_result_is_the_unchanged_report()
    {
        await using var host = NewHost();
        var started = await host.RequestAsync(s => s.StartGenerationAsync(new GenerateRequest { TimeLimitSeconds = 20, Workers = 2 }, CancellationToken.None));
        Assert.Equal("RUNNING", started.State);
        Assert.Equal(32, started.Id.Length);

        var job = await host.WaitForJobAsync(started.Id);
        Assert.Equal("COMPLETED", job.State);
        Assert.Equal("TIMETABLE_GENERATED", job.Outcome);
        Assert.Contains(job.Solver!.Value.GetProperty("status").GetString(), new[] { "FEASIBLE", "OPTIMAL" });
        Assert.Equal(2, job.Summary!.Value.GetProperty("sectionsScheduled").GetInt32());

        // The result is the stored report text itself (RunResultMapperTests proves it equals Report.ToJson()).
        var json = await host.RequestAsync(s => s.GetLatestResultJsonAsync(CancellationToken.None));
        var repository = host.Provider.GetRequiredService<ITimetableRepository>();
        Assert.Equal(await repository.GetReportJsonAsync(Guid.ParseExact(started.Id, "N"), CancellationToken.None), json);
        using var report = System.Text.Json.JsonDocument.Parse(json!);
        Assert.Equal("TIMETABLE_GENERATED", report.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task A_second_generation_while_one_runs_is_rejected()
    {
        await using var host = NewHost();
        Assert.True(host.Runner.TryReserve()); // simulate a running solve
        try
        {
            await Assert.ThrowsAsync<GenerationInProgressException>(() => host.RequestAsync(s => s.StartGenerationAsync(null, CancellationToken.None)));
        }
        finally
        {
            host.Runner.CancelReservation();
        }

        Assert.Equal(0, (await host.RequestAsync(s => s.ListRunsAsync(0, 10, CancellationToken.None))).Total); // nothing stored
    }

    [Fact]
    public async Task Invalid_requests_raise_validation_errors_per_field()
    {
        await using var host = NewHost();
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            host.RequestAsync(s => s.StartGenerationAsync(new GenerateRequest { Profile = "evil", TimeLimitSeconds = 0 }, CancellationToken.None)));
        Assert.Contains("Profile", ex.Errors.Keys);
        Assert.Contains("TimeLimitSeconds", ex.Errors.Keys);

        var paging = await Assert.ThrowsAsync<RequestValidationException>(() => host.RequestAsync(s => s.ListRunsAsync(-1, 0, CancellationToken.None)));
        Assert.Contains("skip", paging.Errors.Keys);
        Assert.Contains("take", paging.Errors.Keys);
    }

    [Fact]
    public async Task Unreadable_input_marks_the_run_failed_with_the_safe_message()
    {
        await using var host = NewHost(() => throw new DataInputException("sections.json: invalid JSON."));
        await Assert.ThrowsAsync<DataInputException>(() => host.RequestAsync(s => s.LoadDataAsync(null, CancellationToken.None)));

        var runs = await host.RequestAsync(s => s.ListRunsAsync(0, 10, CancellationToken.None));
        var run = Assert.Single(runs.Items);
        Assert.Equal("FAILED", run.State);

        var started = await host.RequestAsync(s => s.StartGenerationAsync(null, CancellationToken.None));
        var job = await host.WaitForJobAsync(started.Id);
        Assert.Equal("FAILED", job.State);
        Assert.Equal("sections.json: invalid JSON.", job.Error);
    }
}
