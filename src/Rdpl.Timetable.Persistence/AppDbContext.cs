using Microsoft.EntityFrameworkCore;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence;

/// <summary>
/// EF Core context for the optional PostgreSQL store. All tables live in the <see cref="Schema"/> schema.
/// The solver never touches this context: the API service layer maps solver results to these entities.
/// </summary>
public class AppDbContext : DbContext
{
    public const string Schema = "timetable";

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SectionEntity> Sections => Set<SectionEntity>();

    public DbSet<TeacherEntity> Teachers => Set<TeacherEntity>();

    public DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();

    public DbSet<TimetableRunEntity> TimetableRuns => Set<TimetableRunEntity>();

    public DbSet<TeachingAssignmentEntity> TeachingAssignments => Set<TeachingAssignmentEntity>();

    public DbSet<TimetableEntryEntity> TimetableEntries => Set<TimetableEntryEntity>();

    public DbSet<DataConflictEntity> DataConflicts => Set<DataConflictEntity>();

    public DbSet<SchedulingConflictEntity> SchedulingConflicts => Set<SchedulingConflictEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SnakeCaseNaming.Apply(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// Sets CreatedUtc/UpdatedUtc on master records. PostgreSQL timestamptz requires UTC offsets.
    private void StampAuditColumns()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedUtc == default)
                {
                    entry.Entity.CreatedUtc = now;
                }

                entry.Entity.UpdatedUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedUtc = now;
                entry.Property(e => e.CreatedUtc).IsModified = false;
            }
        }
    }
}
