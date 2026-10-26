namespace Rdpl.Timetable.Persistence.Configurations;

/// <summary>
/// varchar limits. Chosen with headroom over the longest values in the real reports (e.g. the longest activity-group
/// name is 56 characters). Free-text messages use unbounded <c>text</c>.
/// </summary>
internal static class ColumnLengths
{
    public const int Code = 64;
    public const int ShortCode = 32;
    public const int Name = 200;
    public const int Label = 16;
    public const int Path = 260;
    public const int Enum = 32;
}
