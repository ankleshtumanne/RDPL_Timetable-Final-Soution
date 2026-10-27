using System.Text.Json;

namespace Rdpl.Timetable.Api.DTOs;

public sealed record JobDto
{
    /// <summary>32 hex characters (Guid "N" format)</summary>
    public required string Id { get; init; }

    /// <summary>RUNNING, COMPLETED or FAILED.</summary>
    public required string State { get; init; }

    /// <summary>GENERATION or DATA_VALIDATION.</summary>
    public required string Kind { get; init; }

    public required string Profile { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? CompletedUtc { get; init; }

    public string? Outcome { get; init; }

    public string? Error { get; init; }

    public JsonElement? Solver { get; init; }

    public JsonElement? Summary { get; init; }

    public required string Storage { get; init; }

    public required JobLinksDto Links { get; init; }
}

public sealed record JobLinksDto(string Result, string Sections, string Teachers, string Conflicts)
{
    public static readonly JobLinksDto Default = new("/api/timetable/result", "/api/timetable/sections", "/api/timetable/teachers", "/api/timetable/conflicts");
}
