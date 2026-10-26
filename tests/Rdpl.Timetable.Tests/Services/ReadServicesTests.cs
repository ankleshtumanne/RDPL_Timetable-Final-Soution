using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Models;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Repositories;
using Rdpl.Timetable.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Rdpl.Timetable.Tests.Services;

/// <summary>SectionService, TeacherService, ConflictService over the in-memory repositories.</summary>
public sealed class ReadServicesTests
{
    private static ServiceHost NewHost(Func<Synth> school) =>
        ServiceHost.Create(() => school().Dataset(), s => s.AddInMemoryTimetableRepository(10));

    private static async Task<JobDto> GenerateAsync(ServiceHost host)
    {
        var started = await host.RequestAsync(s => s.StartGenerationAsync(new GenerateRequest { TimeLimitSeconds = 20, Workers = 2 }, CancellationToken.None));
        return await host.WaitForJobAsync(started.Id);
    }

    private static Task<T> UseAsync<TService, T>(ServiceHost host, Func<TService, Task<T>> action)
        where TService : notnull
    {
        return Run();

        async Task<T> Run()
        {
            await using var scope = host.Provider.CreateAsyncScope();
            return await action(scope.ServiceProvider.GetRequiredService<TService>());
        }
    }

    [Fact]
    public async Task Section_teacher_and_conflict_views_equal_the_original_report_output()
    {
        await using var host = NewHost(SmallSchool.Build);
        Assert.Equal("TIMETABLE_GENERATED", (await GenerateAsync(host)).Outcome);
        await ReportParity.AssertReadEndpointsMatchReportAsync(host);

        // T1 teaches Mathematics in both sections: 8 lessons, never two in one slot.
        var teachers = await UseAsync<ITeacherService, TeacherTimetablesDto>(host, s => s.GetAllAsync(CancellationToken.None));
        var asha = teachers.TeacherTimetables!["Asha Teacher"];
        Assert.Equal(8, asha.Values.Sum(d => d.Count));
        Assert.All(asha.Values, day => Assert.Equal(day.Count, day.Select(l => l.Period).Distinct().Count()));
    }

    [Fact]
    public async Task Section_lookup_accepts_id_or_display_name_and_reports_unknown_and_excluded_sections()
    {
        await using var host = NewHost(() => SmallSchool.Build().PrePrimarySection("Nursery", "Lily"));
        await GenerateAsync(host);

        var byId = await UseAsync<ISectionService, SectionTimetableDto>(host, s => s.GetAsync("CLASS-4-LILY", CancellationToken.None));
        var byName = await UseAsync<ISectionService, SectionTimetableDto>(host, s => s.GetAsync("grade 4 - lily", CancellationToken.None));
        Assert.Equal("Grade 4 - Lily", byId.Section);
        Assert.Equal("class-4-lily", byName.Id);
        Assert.Equal(8, byId.Timetable.Values.Sum(d => d.Count));

        var unknown = await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<ISectionService, SectionTimetableDto>(host, s => s.GetAsync("class-99-x", CancellationToken.None)));
        Assert.Equal("Section not found.", unknown.Message);

        var excluded = await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<ISectionService, SectionTimetableDto>(host, s => s.GetAsync("nursery-lily", CancellationToken.None)));
        Assert.Equal("Nursery - Lily is excluded from scheduling; see dataConflicts.", excluded.Message);
    }

    [Fact]
    public async Task Infeasible_generation_has_no_timetables_but_its_conflicts_are_served()
    {
        await using var host = NewHost(SmallSchool.Overloaded);
        Assert.Equal("INFEASIBLE", (await GenerateAsync(host)).Outcome);

        var sections = await UseAsync<ISectionService, SectionTimetablesDto>(host, s => s.GetAllAsync(CancellationToken.None));
        Assert.False(sections.Success);
        Assert.Null(sections.SectionTimetables);
        var noTimetable = await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<ISectionService, SectionTimetableDto>(host, s => s.GetAsync("class-4-lily", CancellationToken.None)));
        Assert.Equal("No valid timetable was generated (outcome INFEASIBLE).", noTimetable.Message);
        Assert.Null((await UseAsync<ITeacherService, TeacherTimetablesDto>(host, s => s.GetAllAsync(CancellationToken.None))).TeacherTimetables);

        var conflicts = await UseAsync<IConflictService, ConflictsDto>(host, s => s.GetLatestAsync(CancellationToken.None));
        Assert.NotEmpty(conflicts.SchedulingConflicts);
        await ReportParity.AssertReadEndpointsMatchReportAsync(host);
    }

    [Fact]
    public async Task Before_any_run_the_views_return_not_found_and_conflicts_fall_back_to_load_data()
    {
        await using var host = NewHost(SmallSchool.Build);
        var none = await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<ISectionService, SectionTimetablesDto>(host, s => s.GetAllAsync(CancellationToken.None)));
        Assert.StartsWith("No timetable has been generated yet.", none.Message);
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<ITeacherService, TeacherTimetablesDto>(host, s => s.GetAllAsync(CancellationToken.None)));
        var nothing = await Assert.ThrowsAsync<ResourceNotFoundException>(() => UseAsync<IConflictService, ConflictsDto>(host, s => s.GetLatestAsync(CancellationToken.None)));
        Assert.StartsWith("Nothing has been loaded yet.", nothing.Message);

        var loaded = await host.RequestAsync(s => s.LoadDataAsync(null, CancellationToken.None));
        var conflicts = await UseAsync<IConflictService, ConflictsDto>(host, s => s.GetLatestAsync(CancellationToken.None));
        Assert.Equal("DATA_VALIDATION_ONLY", conflicts.Outcome);
        Assert.Equal(loaded.DataConflicts.Count, conflicts.DataConflicts.Count);
    }

    [Fact]
    public void Repository_conflict_types_match_the_core_normalizer()
    {
        Assert.Equal(DataConflictTypes.SectionExcludedPrePrimary, ConflictTypes.SectionExcludedPrePrimary);
        Assert.Equal(DataConflictTypes.SectionExcludedNoCurriculum, ConflictTypes.SectionExcludedNoCurriculum);
    }
}
