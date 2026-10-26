namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>One entry of the report's schedulingConflicts[] (why the model is or may be infeasible).</summary>
public class SchedulingConflictEntity
{
    public long Id { get; set; }

    public Guid RunId { get; set; }

    public TimetableRunEntity Run { get; set; } = null!;

    /// <summary>e.g. BLOCK_PARITY, L1_DAY_CAPACITY, TEACHER_OVERLOAD, SOLVER_INFEASIBLE.</summary>
    public required string Code { get; set; }

    /// <summary>One or more rule ids, e.g. "H5/B1/H8".</summary>
    public required string RuleId { get; set; }

    public required string Message { get; set; }

    /// <summary>True when this finding alone proves the strict model infeasible.</summary>
    public bool ProvesInfeasibility { get; set; }

    /// <summary>Affected section display names (PostgreSQL text[]).</summary>
    public List<string> Sections { get; set; } = new();

    public string? TeacherCode { get; set; }
}
