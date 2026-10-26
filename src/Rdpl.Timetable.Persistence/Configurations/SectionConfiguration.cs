using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class SectionConfiguration : IEntityTypeConfiguration<SectionEntity>
{
    public void Configure(EntityTypeBuilder<SectionEntity> builder)
    {
        builder.ToTable("sections", t => t.HasCheckConstraint("ck_sections_capacity", "capacity >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.ExternalId).HasMaxLength(ColumnLengths.Code).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(ColumnLengths.Name).IsRequired();
        builder.Property(x => x.Grade).HasMaxLength(ColumnLengths.ShortCode).IsRequired();
        builder.Property(x => x.SectionName).HasMaxLength(ColumnLengths.ShortCode).IsRequired();
        builder.Property(x => x.CurriculumKey).HasMaxLength(ColumnLengths.ShortCode).IsRequired();
        builder.HasIndex(x => x.ExternalId).IsUnique();
        builder.HasIndex(x => x.DisplayName).IsUnique();
    }
}
