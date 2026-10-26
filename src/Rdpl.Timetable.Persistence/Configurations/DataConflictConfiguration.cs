using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class DataConflictConfiguration : IEntityTypeConfiguration<DataConflictEntity>
{
    public void Configure(EntityTypeBuilder<DataConflictEntity> builder)
    {
        builder.ToTable("data_conflicts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Type).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(ColumnLengths.Label).IsRequired();
        builder.Property(x => x.Message).HasColumnType("text").IsRequired();
        builder.Property(x => x.SectionText).HasMaxLength(ColumnLengths.Name);
        builder.Property(x => x.SubjectText).HasMaxLength(ColumnLengths.Name);
        builder.Property(x => x.TeacherCode).HasMaxLength(ColumnLengths.Code);
        builder.Property(x => x.Source).HasMaxLength(ColumnLengths.Path);
        builder.Property(x => x.RuleId).HasMaxLength(ColumnLengths.ShortCode);
        builder.HasIndex(x => new { x.RunId, x.Type });
    }
}
