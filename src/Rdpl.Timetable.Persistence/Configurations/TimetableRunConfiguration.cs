using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class TimetableRunConfiguration : IEntityTypeConfiguration<TimetableRunEntity>
{
    public void Configure(EntityTypeBuilder<TimetableRunEntity> builder)
    {
        builder.ToTable("timetable_runs", t =>
        {
            // Same bounds the API/CLI already enforce (SolverOptions.Validate): a direct DB write cannot bypass them.
            t.HasCheckConstraint("ck_timetable_runs_time_limit", "time_limit_seconds BETWEEN 1 AND 1800");
            t.HasCheckConstraint("ck_timetable_runs_workers", "workers BETWEEN 1 AND 64");
            t.HasCheckConstraint("ck_timetable_runs_wall_time", "wall_time_ms IS NULL OR wall_time_ms >= 0");
            t.HasCheckConstraint("ck_timetable_runs_completed", "completed_utc IS NULL OR completed_utc >= created_utc");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(ColumnLengths.Enum).IsRequired();
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(ColumnLengths.Enum).IsRequired();
        builder.Property(x => x.Profile).HasMaxLength(ColumnLengths.ShortCode).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(ColumnLengths.Code);
        builder.Property(x => x.DataSourceKind).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.DataSourceWarning).HasColumnType("text");
        builder.Property(x => x.AssignmentsWorkbook).HasMaxLength(ColumnLengths.Path);
        builder.Property(x => x.SolverStatus).HasMaxLength(ColumnLengths.ShortCode);
        builder.Property(x => x.Error).HasColumnType("text");
        builder.Property(x => x.ReportJson).HasColumnType("json");
        builder.Property(x => x.SolverJson).HasColumnType("json");
        builder.Property(x => x.SummaryJson).HasColumnType("json");
        builder.Property(x => x.DiagnosticJson).HasColumnType("json");

        // Optimistic concurrency on PostgreSQL's xmin system column. The name and type are set explicitly: Npgsql would
        // infer them late in model finalization, after SnakeCaseNaming has run, and the column would become "version".
        builder.Property(x => x.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.HasIndex(x => x.CreatedUtc).IsDescending();
        builder.HasIndex(x => new { x.Kind, x.State });
        builder.HasIndex(x => new { x.Kind, x.State, x.CompletedUtc }).IsDescending(false, false, true);

        builder.HasMany(x => x.TeachingAssignments).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.TimetableEntries).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.DataConflicts).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.SchedulingConflicts).WithOne(x => x.Run).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
