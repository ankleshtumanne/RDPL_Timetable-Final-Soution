using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<SubjectEntity>
{
    public void Configure(EntityTypeBuilder<SubjectEntity> builder)
    {
        builder.ToTable("subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Name).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(ColumnLengths.Enum).IsRequired();
        builder.HasIndex(x => x.NormalizedName).IsUnique();
    }
}
