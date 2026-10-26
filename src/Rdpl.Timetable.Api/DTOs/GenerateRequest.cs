namespace Rdpl.Timetable.Api.DTOs;

/// <summary>
/// Body of POST /api/timetable/generate and /load-data. Every field is optional and bounded.
/// There is deliberately no field for file paths: the API only ever reads server-configured files.
/// </summary>
public sealed record GenerateRequest
{
    /// <summary>"strict" (every Tier-1/Tier-2 rule hard) or "pragmatic" (H5 soft, L1 soft for Class 11/12).</summary>
    public string? Profile { get; init; }

    public int? TimeLimitSeconds { get; init; }

    public int? Workers { get; init; }

    public int? RandomSeed { get; init; }

    /// <summary>"Exclude" (default, H10) or "TreatAsAssigned".</summary>
    public string? ZeroWorkloadPolicy { get; init; }

    /// <summary>"ReserveSlot" (default) or "Exclude".</summary>
    public string? UnstaffedPolicy { get; init; }

    public bool? SoftObjective { get; init; }

    public bool? RunDiagnosticOnInfeasible { get; init; }
}
