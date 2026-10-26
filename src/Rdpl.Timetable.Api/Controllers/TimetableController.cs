using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;

namespace Rdpl.Timetable.Api.Controllers;

/// <summary>
/// Timetable runs: load/validate input, start a generation, poll it, fetch the result, list runs.
/// Routes are identical to the original minimal API (/api/timetable/...). Authentication: the global fallback policy
/// (X-Api-Key) applies to every action. Errors are produced by the centralized SafeExceptionHandler.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TimetableController : ControllerBase
{
    private readonly ITimetableService _service;

    public TimetableController(ITimetableService service)
    {
        _service = service;
    }

    /// <summary>Loads and validates the configured dataset (no solver). The body is optional.</summary>
    [HttpPost("load-data")]
    [ProducesResponseType(typeof(LoadDataResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LoadDataResponseDto>> LoadData(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GenerateRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await _service.LoadDataAsync(request, cancellationToken).ConfigureAwait(false));

    /// <summary>Starts a background generation. 202 with a Location to poll; 409 while another one runs.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobDto>> Generate(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GenerateRequest? request,
        CancellationToken cancellationToken)
    {
        var job = await _service.StartGenerationAsync(request, cancellationToken).ConfigureAwait(false);
        return Accepted($"/api/timetable/jobs/{job.Id}", job);
    }

    [HttpGet("jobs/{id}")]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobDto>> GetJob(string id, CancellationToken cancellationToken)
    {
        if (!RouteIds.IsSafe(id))
        {
            return Problem(detail: "Invalid job id.", statusCode: StatusCodes.Status400BadRequest);
        }

        var job = Guid.TryParseExact(id, "N", out var runId) || Guid.TryParse(id, out runId)
            ? await _service.GetJobAsync(runId, cancellationToken).ConfigureAwait(false)
            : null;
        return job is null ? Problem(detail: "Job not found.", statusCode: StatusCodes.Status404NotFound) : Ok(job);
    }

    /// <summary>Full report of the latest finished generation — the same JSON the CLI writes.</summary>
    [HttpGet("result")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResult(CancellationToken cancellationToken)
    {
        var json = await _service.GetLatestResultJsonAsync(cancellationToken).ConfigureAwait(false);
        return json is null
            ? Problem(detail: "No timetable has been generated yet. Call POST /api/timetable/generate and poll the job.", statusCode: StatusCodes.Status404NotFound)
            : Content(json, "application/json");
    }

    /// <summary>Run history, newest first (paged).</summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PagedResultDto<RunListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<RunListItemDto>>> ListRuns([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        Ok(await _service.ListRunsAsync(skip, take, cancellationToken).ConfigureAwait(false));
}
