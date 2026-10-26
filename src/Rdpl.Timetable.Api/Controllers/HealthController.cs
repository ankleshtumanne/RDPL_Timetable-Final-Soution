using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Rdpl.Timetable.Api.Controllers;

/// <summary>
/// Liveness probe: anonymous, no dependencies, no details. Readiness (including the database check) is
/// GET /health/ready, served by the ASP.NET Core health-check middleware.
/// </summary>
[ApiController]
[Route("health")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { status = "ok" });
}
