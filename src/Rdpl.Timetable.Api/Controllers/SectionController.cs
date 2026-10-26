using Microsoft.AspNetCore.Mvc;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Infrastructure;
using Rdpl.Timetable.Api.Services.Interfaces;

namespace Rdpl.Timetable.Api.Controllers;

/// <summary>
/// Section-wise timetables of the latest completed generation.
/// Routes: /api/section (from [controller]) and the original /api/timetable/sections, kept so existing clients keep working.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SectionController : ControllerBase
{
    private readonly ISectionService _service;

    public SectionController(ISectionService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("~/api/timetable/sections")]
    [ProducesResponseType(typeof(SectionTimetablesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionTimetablesDto>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken).ConfigureAwait(false));

    /// <param name="id">sections.json id ("class-10-lily") or display name ("Grade 10 - Lily").</param>
    [HttpGet("{id}")]
    [HttpGet("~/api/timetable/sections/{id}")]
    [ProducesResponseType(typeof(SectionTimetableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionTimetableDto>> Get(string id, CancellationToken cancellationToken)
    {
        if (!RouteIds.IsSafe(id))
        {
            return Problem(detail: "Invalid section id.", statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await _service.GetAsync(id, cancellationToken).ConfigureAwait(false));
    }
}
