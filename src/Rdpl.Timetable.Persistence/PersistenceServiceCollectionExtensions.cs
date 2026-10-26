using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Rdpl.Timetable.Persistence.Options;
using Rdpl.Timetable.Persistence.Repositories.Implementations;
using Rdpl.Timetable.Persistence.Repositories.Interfaces;

namespace Rdpl.Timetable.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> and the PostgreSQL repositories (scoped). Validates the options and the connection string
    /// up front and fails closed with messages that never contain the connection string or password.
    /// </summary>
    public static IServiceCollection AddTimetablePersistence(this IServiceCollection services, string connectionString, PersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }

        var builder = ParseConnectionString(connectionString, options.ConnectionStringName);

        services.AddDbContext<AppDbContext>(db =>
        {
            db.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", AppDbContext.Schema);
                npgsql.CommandTimeout(options.CommandTimeoutSeconds);
                if (options.MaxRetryCount > 0)
                {
                    npgsql.EnableRetryOnFailure(options.MaxRetryCount, TimeSpan.FromSeconds(options.MaxRetryDelaySeconds), null);
                }
            });

            // Never log parameter values (they can contain personal data) and keep detailed errors off.
            db.EnableSensitiveDataLogging(false);
            db.EnableDetailedErrors(false);
        });

        services.AddScoped<ITimetableRepository, TimetableRepository>();
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IConflictRepository, ConflictRepository>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory repository used when persistence is disabled (original API behaviour: results kept in
    /// memory and lost on restart).
    /// </summary>
    public static IServiceCollection AddInMemoryTimetableRepository(this IServiceCollection services, int maxRetainedRuns)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new InMemoryRunStore(maxRetainedRuns));
        services.AddSingleton<ITimetableRepository, InMemoryTimetableRepository>();
        services.AddSingleton<ISectionRepository, InMemorySectionRepository>();
        services.AddSingleton<ITeacherRepository, InMemoryTeacherRepository>();
        services.AddSingleton<IConflictRepository, InMemoryConflictRepository>();
        return services;
    }

    /// <summary>Host/port/database/user only — safe for logs. Never includes the password.</summary>
    public static string DescribeSafely(string connectionString)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(connectionString);
            return $"Host={b.Host};Port={b.Port};Database={b.Database};Username={b.Username}";
        }
        catch (ArgumentException)
        {
            return "(unparseable connection string)";
        }
    }

    internal static NpgsqlConnectionStringBuilder ParseConnectionString(string connectionString, string name)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(MissingMessage(name));
        }

        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Do not echo the value: it may contain the password.
            throw new InvalidOperationException($"ConnectionStrings:{name} is not a valid PostgreSQL connection string.");
        }

        if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Database) || string.IsNullOrWhiteSpace(builder.Username))
        {
            throw new InvalidOperationException($"ConnectionStrings:{name} must specify Host, Database and Username.");
        }

        if (builder.IncludeErrorDetail)
        {
            // Error detail can echo row values into exception messages and logs.
            throw new InvalidOperationException($"ConnectionStrings:{name} must not set 'Include Error Detail=true'.");
        }

        return builder;
    }

    public static string MissingMessage(string name) =>
        $"Persistence:Enabled is true but ConnectionStrings:{name} is not configured. " +
        $"Development: dotnet user-secrets set \"ConnectionStrings:{name}\" \"Host=...;Database=...;Username=...;Password=...\". " +
        $"Production: environment variable ConnectionStrings__{name} or Azure Key Vault secret ConnectionStrings--{name}.";
}
