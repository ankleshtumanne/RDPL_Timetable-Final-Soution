namespace Rdpl.Timetable.Persistence.Options;

/// <summary>
/// Non-secret database settings (appsettings.json section "Persistence").
/// The connection string is a SECRET and is never stored here: it is read from <c>ConnectionStrings:{ConnectionStringName}</c>,
/// supplied by user-secrets (Development), the environment variable <c>ConnectionStrings__Timetable</c>, or the
/// Azure Key Vault secret <c>ConnectionStrings--Timetable</c>.
/// </summary>
public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    /// <summary>
    /// Off by default: the API then behaves exactly as before (file inputs, in-memory results) and needs no database.
    /// </summary>
    public bool Enabled { get; set; }

    public string ConnectionStringName { get; set; } = "Timetable";

    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>Transient-failure retries (Npgsql execution strategy). 0 disables retries.</summary>
    public int MaxRetryCount { get; set; } = 3;

    public int MaxRetryDelaySeconds { get; set; } = 10;

    /// <summary>Returns user-safe error messages; empty when the options are valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(ConnectionStringName) || ConnectionStringName.Length > 64)
        {
            errors.Add("Persistence:ConnectionStringName must be 1–64 characters.");
        }

        if (CommandTimeoutSeconds is < 1 or > 600)
        {
            errors.Add("Persistence:CommandTimeoutSeconds must be between 1 and 600.");
        }

        if (MaxRetryCount is < 0 or > 10)
        {
            errors.Add("Persistence:MaxRetryCount must be between 0 and 10.");
        }

        if (MaxRetryDelaySeconds is < 1 or > 60)
        {
            errors.Add("Persistence:MaxRetryDelaySeconds must be between 1 and 60.");
        }

        return errors;
    }
}
