namespace Rdpl.Timetable.Persistence.Repositories;

/// <summary>
/// Data-conflict types the repositories query by. Values are the ones written by the Core normalizer
/// (Rdpl.Timetable.Models.DataConflictTypes); a test asserts they stay identical.
/// </summary>
public static class ConflictTypes
{
    public const string SectionExcludedPrePrimary = "SECTION_EXCLUDED_PRE_PRIMARY";
    public const string SectionExcludedNoCurriculum = "SECTION_EXCLUDED_NO_CURRICULUM";

    public static bool IsSectionExclusion(string type) => type is SectionExcludedPrePrimary or SectionExcludedNoCurriculum;
}
