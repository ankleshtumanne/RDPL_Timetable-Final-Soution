using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Options;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Data;

namespace Rdpl.Timetable.Tests.TestSupport;

/// <summary>
/// The API's service graph (runner, service, repository) built with the same registrations as Program.cs, but with
/// an in-test dataset instead of files. No HTTP involved.
/// </summary>
internal sealed class ServiceHost : IAsyncDisposable
{
    private ServiceHost(ServiceProvider provider) => Provider = provider;

    public ServiceProvider Provider { get; }

    public ITimetableJobRunner Runner => Provider.GetRequiredService<ITimetableJobRunner>();

    public static ServiceHost Create(Func<LoadedDataset> dataset, Action<IServiceCollection> addRepository)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<TimetableApiOptions>(o =>
        {
            o.DefaultTimeLimitSeconds = 20;
            o.DefaultWorkers = 2;
            o.MaxWorkers = 4;
        });
        services.AddSingleton<IDatasetProvider>(new DelegateDatasetProvider(dataset));
        services.AddSingleton<IHostApplicationLifetime, NoopLifetime>();
        services.AddTimetableServices(); // the exact registrations Program.cs uses
        addRepository(services);
        return new ServiceHost(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
    }

    /// <summary>Runs <paramref name="action"/> with a fresh scope, like one HTTP request.</summary>
    public async Task<T> RequestAsync<T>(Func<ITimetableService, Task<T>> action)
    {
        await using var scope = Provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ITimetableService>());
    }

    public async Task<JobDto> WaitForJobAsync(string id, int timeoutSeconds = 120)
    {
        var runId = Guid.ParseExact(id, "N");
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var job = await RequestAsync(s => s.GetJobAsync(runId, CancellationToken.None));
            if (job is not null && job.State != "RUNNING" && !Runner.IsBusy)
            {
                return job;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Job {id} did not finish within {timeoutSeconds}s.");
    }

    public ValueTask DisposeAsync() => Provider.DisposeAsync();

    private sealed class DelegateDatasetProvider : IDatasetProvider
    {
        private readonly Func<LoadedDataset> _load;

        public DelegateDatasetProvider(Func<LoadedDataset> load) => _load = load;

        public LoadedDataset Load() => _load();
    }

    private sealed class NoopLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}

/// <summary>Small feasible school used by the service and repository tests.</summary>
internal static class SmallSchool
{
    /// <summary>
    /// Two sections, 8 slots each. T1 teaches Mathematics in BOTH sections (cross-section H1 coupling), T2/T3 Science.
    /// Also: one UNASSIGNED-TT row, one row for an unknown section and one orphan subject — all must be reported.
    /// </summary>
    public static Synth Build() => new Synth(Synth.SmallBell(days: 2))
        .Section("Class 4", "Lily").Section("Class 4", "Lavender")
        .Subject("Class 4", "Mathematics", 2, 4).Subject("Class 4", "Science", 2, 4)
        .Assign("Grade 4 - Lily", "Mathematics", "T1", teacherName: "Asha Teacher").Assign("Grade 4 - Lavender", "Mathematics", "T1", teacherName: "Asha Teacher")
        .Assign("Grade 4 - Lily", "Science", "T2").Assign("Grade 4 - Lavender", "Science", "T3")
        .Assign("Grade 4 - Lily", "Science", "UNASSIGNED-TT", teacherName: "Unassigned Teacher")
        .Assign("Grade 9 - Rose", "Mathematics", "T9")
        .Assign("Grade 4 - Lily", "Dance", "T4");

    /// <summary>T1 must teach 16 periods in 8 slots: provably infeasible.</summary>
    public static Synth Overloaded() => new Synth(Synth.SmallBell(days: 2))
        .Section("Class 4", "Lily").Section("Class 4", "Lavender")
        .Subject("Class 4", "Mathematics", 2, 4).Subject("Class 4", "Science", 2, 4)
        .Assign("Grade 4 - Lily", "Mathematics", "T1").Assign("Grade 4 - Lavender", "Mathematics", "T1")
        .Assign("Grade 4 - Lily", "Science", "T1").Assign("Grade 4 - Lavender", "Science", "T1");
}
