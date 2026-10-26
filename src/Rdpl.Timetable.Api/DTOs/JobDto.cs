using System.Text.Json;

namespace Rdpl.Timetable.Api.DTOs;

/// <summary>
/// State of one run, returned by POST /api/timetable/generate (202) and GET /api/timetable/jobs/{id}.
/// Same fields as before the controller refactoring; <see cref="Kind"/> and <see cref="Storage"/> are additions.
/// </summary>
public sealed record JobDto
{
    /// <summary>32 hex characters (Guid "N" format), unchanged from the original API.</summary>
    public required string Id { get; init; }

    /// <summary>RUNNING, COMPLETED or FAILED.</summary>
    public required string State { get; init; }

    /// <summary>GENERATION or DATA_VALIDATION.</summary>
    public required string Kind { get; init; }

    public required string Profile { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? CompletedUtc { get; init; }

    public string? Outcome { get; init; }

    /// <summary>User-safe failure message (never a stack trace).</summary>
    public string? Error { get; init; }

    /// <summary>The report's "solver" object, verbatim.</summary>
    public JsonElement? Solver { get; init; }

    /// <summary>The report's "summary" object, verbatim.</summary>
    public JsonElement? Summary { get; init; }

    /// <summary>"postgresql" when results survive a restart, otherwise "memory".</summary>
    public required string Storage { get; init; }

    public required JobLinksDto Links { get; init; }
}

public sealed record JobLinksDto(string Result, string Sections, string Teachers, string Conflicts)
{
    public static readonly JobLinksDto Default = new("/api/timetable/result", "/api/timetable/sections", "/api/timetable/teachers", "/api/timetable/conflicts");
}
