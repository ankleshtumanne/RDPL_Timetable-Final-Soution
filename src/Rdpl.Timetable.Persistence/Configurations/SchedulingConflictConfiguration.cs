using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class SchedulingConflictConfiguration : IEntityTypeConfiguration<SchedulingConflictEntity>
{
    public void Configure(EntityTypeBuilder<SchedulingConflictEntity> builder)
    {
        builder.ToTable("scheduling_conflicts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Code).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.RuleId).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.Message).HasColumnType("text").IsRequired();
        builder.Property(x => x.Sections).HasColumnType("text[]");
        builder.Property(x => x.TeacherCode).HasMaxLength(ColumnLengths.Code);
        builder.HasIndex(x => new { x.RunId, x.ProvesInfeasibility });
    }
}
