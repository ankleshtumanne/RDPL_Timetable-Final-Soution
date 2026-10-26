using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<TeacherEntity>
{
    public void Configure(EntityTypeBuilder<TeacherEntity> builder)
    {
        builder.ToTable("teachers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Code).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
