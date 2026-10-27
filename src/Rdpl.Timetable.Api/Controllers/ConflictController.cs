using Microsoft.AspNetCore.Mvc;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Services.Interfaces;

namespace Rdpl.Timetable.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ConflictController : ControllerBase
{
    private readonly IConflictService _service;

    public ConflictController(IConflictService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("~/api/timetable/conflicts")]
    [ProducesResponseType(typeof(ConflictsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConflictsDto>> GetLatest(CancellationToken cancellationToken) =>
        Ok(await _service.GetLatestAsync(cancellationToken).ConfigureAwait(false));
}
