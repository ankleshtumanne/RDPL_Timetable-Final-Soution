using System.Text.Json;
using Microsoft.Extensions.Options;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Options;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Data;
using Rdpl.Timetable.Persistence.Entities;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;
using Rdpl.Timetable.Persistence.Repositories.Models;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>Scoped implementation of <see cref="ITimetableService"/>. The solver itself is the unchanged Core pipeline.</summary>
public sealed class TimetableService : ITimetableService
{
    public const int MaxPageSize = 100;

    private readonly ITimetableRepository _repository;
    private readonly ITimetableJobRunner _runner;
    private readonly IDatasetProvider _datasets;
    private readonly ILogger<TimetableService> _logger;
    private readonly IOptionsMonitor<TimetableApiOptions> _options;

    public TimetableService(ITimetableRepository repository, ITimetableJobRunner runner, ILogger<TimetableService> logger, IDatasetProvider datasets, IOptionsMonitor<TimetableApiOptions> options)
    {
        _repository = repository;
        _runner = runner;
        _datasets = datasets;
        _options = options;
        _logger = logger;
    }

    private string Storage => _repository.IsDurable ? "postgresql" : "memory";

    public async Task<LoadDataResponseDto> LoadDataAsync(GenerateRequest? request, CancellationToken cancellationToken)
    {
        var settings = Validate(request);
        var runId = Guid.NewGuid();
        await _repository.AddRunAsync(NewRun(runId, RunKind.DataValidation, settings), cancellationToken).ConfigureAwait(false);

        // From here on the run exists, so EVERY failure must close it; otherwise it stays RUNNING forever.
        TimetableRun run;
        try
        {
            var dataset = _datasets.Load();
            run = TimetableEngine.Run(dataset, settings.Rules, settings.Solver, solve: false, cancellationToken);

            // The work is done: store it even if the client disconnects now (no request token here).
            await _repository.CompleteRunAsync(RunResultMapper.Map(runId, dataset, run, settings.Rules, DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = ex switch
            {
                DataInputException => ex.Message, // user-safe by design
                OperationCanceledException => "The request was cancelled.",
                _ => "Unexpected server error while loading the data. See server logs for run " + runId.ToString("N") + ".",
            };
            await TryFailRunAsync(runId, message).ConfigureAwait(false);
            throw; // the centralized exception handler turns it into 422 / 499 / 503 / 500
        }

        return LoadDataResponseDto.From(runId, Storage, run.Report);
    }

    /// <summary>Marks a run failed without hiding the original exception if the store itself is unavailable.</summary>
    private async Task TryFailRunAsync(Guid runId, string message)
    {
        try
        {
            await _repository.FailRunAsync(runId, message, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // PostgreSQL mode: RunRecoveryHostedService marks it failed at the next start.
            _logger.LogError(ex, "Run {RunId}: could not record the failure.", runId);
        }
    }

    public async Task<JobDto> StartGenerationAsync(GenerateRequest? request, CancellationToken cancellationToken)
    {
        var settings = Validate(request);
        if (!_runner.TryReserve())
        {
            throw new GenerationInProgressException();
        }

        var runId = Guid.NewGuid();
        var newRun = NewRun(runId, RunKind.Generation, settings);
        try
        {
            await _repository.AddRunAsync(newRun, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _runner.CancelReservation();
            throw;
        }

        _runner.Start(runId, settings);
        return new JobDto
        {
            Id = runId.ToString("N"),
            State = "RUNNING",
            Kind = "GENERATION",
            Profile = newRun.Profile,
            CreatedUtc = newRun.CreatedUtc,
            Storage = Storage,
            Links = JobLinksDto.Default,
        };
    }

    public async Task<JobDto?> GetJobAsync(Guid runId, CancellationToken cancellationToken)
    {
        var record = await _repository.GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
        return record is null ? null : ToJob(record);
    }

    public async Task<string?> GetLatestResultJsonAsync(CancellationToken cancellationToken)
    {
        var latest = await _repository.GetLatestCompletedRunAsync(RunKind.Generation, cancellationToken).ConfigureAwait(false);
        return latest is null ? null : await _repository.GetReportJsonAsync(latest.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResultDto<RunListItemDto>> ListRunsAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (skip < 0 || skip > 1_000_000)
        {
            errors["skip"] = new[] { "Must be between 0 and 1000000." };
        }

        if (take < 1 || take > MaxPageSize)
        {
            errors["take"] = new[] { $"Must be between 1 and {MaxPageSize}." };
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }

        var items = await _repository.ListRunsAsync(skip, take, cancellationToken).ConfigureAwait(false);
        var total = await _repository.CountRunsAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResultDto<RunListItemDto>(items.Select(ToListItem).ToList(), skip, take, total, Storage);
    }

    private RunSettings Validate(GenerateRequest? request)
    {
        var (settings, errors) = GenerateRequestValidator.Validate(request, _options.CurrentValue);
        return settings ?? throw new RequestValidationException(errors);
    }

    private static NewRun NewRun(Guid id, RunKind kind, RunSettings settings) =>
        new(id, kind, settings.Rules.Profile, DateTimeOffset.UtcNow, settings.Solver.TimeLimitSeconds, settings.Solver.Workers, settings.Solver.RandomSeed);

    private JobDto ToJob(RunRecord r) => new()
    {
        Id = r.Id.ToString("N"),
        State = r.State.ToString().ToUpperInvariant(),
        Kind = KindText(r.Kind),
        Profile = r.Profile,
        CreatedUtc = r.CreatedUtc,
        CompletedUtc = r.CompletedUtc,
        Outcome = r.Outcome,
        Error = r.Error,
        Solver = ParseJson(r.SolverJson),
        Summary = ParseJson(r.SummaryJson),
        Storage = Storage,
        Links = JobLinksDto.Default,
    };

    private static RunListItemDto ToListItem(RunRecord r) => new(
        r.Id.ToString("N"), KindText(r.Kind), r.State.ToString().ToUpperInvariant(), r.Profile, r.CreatedUtc, r.CompletedUtc, r.Outcome, r.Success,
        r.DataSourceKind, r.UsesDummyData, r.SolverStatus, r.WallTimeMs, r.HardConstraintsSatisfied, r.SectionsScheduled, r.TotalSlotsScheduled,
        r.LessonCount, r.DataConflictCount, r.SchedulingConflictCount);

    private static string KindText(RunKind kind) => kind == RunKind.Generation ? "GENERATION" : "DATA_VALIDATION";

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
