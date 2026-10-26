using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Implementations;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Tests.Persistence;

public sealed class InMemoryRepositoryTests
{
    [Fact]
    public async Task Retention_never_evicts_the_latest_completed_run_of_each_kind()
    {
        var repository = new InMemoryTimetableRepository(new InMemoryRunStore(3));
        var t0 = DateTimeOffset.UtcNow;
        var generation = await AddCompletedAsync(repository, RunKind.Generation, t0);
        for (var i = 1; i <= 10; i++)
        {
            await AddCompletedAsync(repository, RunKind.DataValidation, t0.AddMinutes(i));
        }

        Assert.Equal(generation, (await repository.GetLatestCompletedRunAsync(RunKind.Generation, CancellationToken.None))!.Id);
        Assert.Equal(4, await repository.CountRunsAsync(CancellationToken.None)); // 3 newest + the protected generation
    }

    [Fact]
    public async Task Retention_still_drops_older_generations()
    {
        var repository = new InMemoryTimetableRepository(new InMemoryRunStore(2));
        var t0 = DateTimeOffset.UtcNow;
        var first = await AddCompletedAsync(repository, RunKind.Generation, t0);
        await AddCompletedAsync(repository, RunKind.Generation, t0.AddMinutes(1));
        var newest = await AddCompletedAsync(repository, RunKind.Generation, t0.AddMinutes(2));

        Assert.Null(await repository.GetRunAsync(first, CancellationToken.None));
        Assert.Equal(newest, (await repository.GetLatestCompletedRunAsync(RunKind.Generation, CancellationToken.None))!.Id);
    }

    private static async Task<Guid> AddCompletedAsync(InMemoryTimetableRepository repository, RunKind kind, DateTimeOffset created)
    {
        var id = Guid.NewGuid();
        await repository.AddRunAsync(new NewRun(id, kind, "strict", created, 60, 2, 1), CancellationToken.None);
        await repository.CompleteRunAsync(new RunResult
        {
            RunId = id,
            State = RunState.Completed,
            CompletedUtc = created.AddSeconds(1),
            DataSourceKind = "TEST",
            ReportJson = "{}",
        }, CancellationToken.None);
        return id;
    }
}