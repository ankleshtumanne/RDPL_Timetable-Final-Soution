namespace Rdpl.Timetable.Persistence.Entities;

/// <summary>Master records whose timestamps are maintained by <see cref="AppDbContext"/> on save.</summary>
public interface IAuditedEntity
{
    DateTimeOffset CreatedUtc { get; set; }

    DateTimeOffset UpdatedUtc { get; set; }
}
