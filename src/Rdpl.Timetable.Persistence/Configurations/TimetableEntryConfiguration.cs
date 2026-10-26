using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rdpl.Timetable.Persistence.Entities;

namespace Rdpl.Timetable.Persistence.Configurations;

internal sealed class TimetableEntryConfiguration : IEntityTypeConfiguration<TimetableEntryEntity>
{
    /// <summary>Stores the day as "MONDAY" … "SATURDAY", the same spelling as bell-schedule.json and the JSON report.</summary>
    internal static readonly ValueConverter<TimetableDay, string> DayConverter = new(
        day => day.ToString().ToUpperInvariant(),
        text => Enum.Parse<TimetableDay>(text, true));

    public void Configure(EntityTypeBuilder<TimetableEntryEntity> builder)
    {
        builder.ToTable("timetable_entries", t =>
        {
            t.HasCheckConstraint("ck_timetable_entries_period", "period BETWEEN 1 AND 12");
            t.HasCheckConstraint("ck_timetable_entries_day", "day IN ('MONDAY','TUESDAY','WEDNESDAY','THURSDAY','FRIDAY','SATURDAY')");
            t.HasCheckConstraint("ck_timetable_entries_times", "end_time > start_time");
            t.HasCheckConstraint("ck_timetable_entries_lesson_type", "lesson_type IN ('SUBJECT','ACTIVITY','RESERVED_UNSTAFFED')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityAlwaysColumn();
        builder.Property(x => x.Day).HasConversion(DayConverter).HasMaxLength(ColumnLengths.Label).IsRequired();
        builder.Property(x => x.Slot).HasMaxLength(ColumnLengths.Label).IsRequired();
        builder.Property(x => x.LessonType).HasMaxLength(ColumnLengths.Enum).IsRequired();
        builder.Property(x => x.Instructors).HasColumnType("text[]");
        builder.Property(x => x.Note).HasColumnType("text");
        builder.Property(x => x.TeacherName).HasMaxLength(ColumnLengths.Name);

        builder.HasOne(x => x.Section).WithMany(x => x.TimetableEntries).HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany(x => x.TimetableEntries).HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Teacher).WithMany(x => x.TimetableEntries).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);

        // H3 re-checked by the database: one lesson per section per teaching slot.
        builder.HasIndex(x => new { x.RunId, x.SectionId, x.Day, x.Period })
            .IsUnique()
            .HasDatabaseName("ux_timetable_entries_section_slot");

        // H1 re-checked by the database: a booked teacher appears at most once per slot. Activities and reserved slots
        // have no teacher (T6/AC8, DATA-1), hence the filter.
        builder.HasIndex(x => new { x.RunId, x.TeacherId, x.Day, x.Period })
            .IsUnique()
            .HasFilter("teacher_id IS NOT NULL")
            .HasDatabaseName("ux_timetable_entries_teacher_slot");

        builder.HasIndex(x => new { x.RunId, x.SubjectId });
    }
}
