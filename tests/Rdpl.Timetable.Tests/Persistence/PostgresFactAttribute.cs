namespace Rdpl.Timetable.Tests.Persistence;

/// <summary>
/// Runs only when <c>TIMETABLE_TEST_POSTGRES</c> holds an ADMIN connection string to a disposable PostgreSQL server
/// (the test creates and drops its own database). Otherwise the test is reported as SKIPPED, never as passed.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "TIMETABLE_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} to an admin connection string of a disposable PostgreSQL server to run this test.";
        }
    }
}
