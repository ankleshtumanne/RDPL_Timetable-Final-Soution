using System.Text.RegularExpressions;

namespace Rdpl.Timetable.Api.Infrastructure;

/// <summary>Validation of ids taken from the URL (unchanged rules from the original API).</summary>
public static partial class RouteIds
{
    public static bool IsSafe(string? id) => id is { Length: > 0 and <= 64 } && SafeId().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9 _-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 50)]
    private static partial Regex SafeId();
}
