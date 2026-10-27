using Microsoft.AspNetCore.Mvc;
using Rdpl.Timetable.Api.DTOs;
using Rdpl.Timetable.Api.Services.Interfaces;

namespace Rdpl.Timetable.Api.Controllers;

/// <summary>
/// Teacher-wise timetables of the latest completed generation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TeacherController : ControllerBase
{
    private readonly ITeacherService _service;

    public TeacherController(ITeacherService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("~/api/timetable/teachers")]
    [ProducesResponseType(typeof(TeacherTimetablesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeacherTimetablesDto>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken).ConfigureAwait(false));
}
