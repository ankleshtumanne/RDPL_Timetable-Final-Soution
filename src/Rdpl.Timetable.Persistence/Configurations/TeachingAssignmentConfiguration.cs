using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class TeachingAssignmentConfiguration : IEntityTypeConfiguration<TeachingAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<TeachingAssignmentEntity> builder)
    {
        // No check on workload_per_week: bad input values (e.g. negative) must be stored as read and reported, not rejected.
        builder.ToTable("teaching_assignments", t => t.HasCheckConstraint("ck_teaching_assignments_source_row", "source_row >= 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.SectionText).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.SubjectText).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.TeacherCode).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.TeacherName).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.SourceFile).HasMaxLength(ColumnLengths.Path).IsRequired();
        builder.Property(x => x.SourceSheet).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.DataOrigin).HasMaxLength(ColumnLengths.Code).IsRequired();

        // Master rows are never deleted while a run references them.
        builder.HasOne(x => x.Section).WithMany(x => x.TeachingAssignments).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany(x => x.TeachingAssignments).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Teacher).WithMany(x => x.TeachingAssignments).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);

        // Explicit name: the generated one is longer than PostgreSQL's 63-character identifier limit.
        builder.HasIndex(x => new { x.RunId, x.SourceFile, x.SourceSheet, x.SourceRow })
            .IsUnique()
            .HasDatabaseName("ux_teaching_assignments_run_source_row");
        builder.HasIndex(x => new { x.RunId, x.TeacherId });
    }
}
