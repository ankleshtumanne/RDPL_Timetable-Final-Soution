using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Rdpl.Timetable.Api.Health;
using Rdpl.Timetable.Api.Options;
using Rdpl.Timetable.Api.Security;
using Rdpl.Timetable.Api.Services.Implementations;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Options;
using Rdpl.Timetable.Reporting;
#if ENABLE_KEY_VAULT
using Azure.Identity;
#endif

namespace Rdpl.Timetable.Api.Infrastructure;

/// <summary>
/// Everything Program.cs used to configure inline, grouped by concern so Program.cs stays a short list of calls.
/// Behaviour is unchanged: same secrets handling, fail-closed checks, hardening, middleware order and endpoints.
/// </summary>
public static class ApiHostingExtensions
{
    /// <summary>
    /// Optional Azure Key Vault configuration source (compiled only with -p:EnableKeyVault=true, used when KeyVault:Uri is
    /// set). Order (later wins): appsettings.json → appsettings.{Env}.json → user-secrets (Development) → environment
    /// variables → command line → Key Vault. Secret "Api--ApiKey" maps to "Api:ApiKey", "ConnectionStrings--Timetable"
    /// to "ConnectionStrings:Timetable".
    /// </summary>
    public static WebApplicationBuilder AddTimetableConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
#if ENABLE_KEY_VAULT
        if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } vaultUri)
        {
            // Managed identity in Azure, developer credentials locally.
            builder.Configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
        }
#endif
        builder.Services.Configure<TimetableApiOptions>(builder.Configuration.GetSection(TimetableApiOptions.SectionName));
        builder.Services.Configure<ApiSecurityOptions>(builder.Configuration.GetSection(ApiSecurityOptions.SectionName));
        builder.Services.Configure<PersistenceOptions>(builder.Configuration.GetSection(PersistenceOptions.SectionName));
        return builder;
    }

    /// <summary>API-key authentication (fallback policy: every endpoint requires it unless marked anonymous), rate limit, Kestrel limits.</summary>
    public static WebApplicationBuilder AddTimetableSecurity(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var security = builder.Configuration.GetSection(ApiSecurityOptions.SectionName).Get<ApiSecurityOptions>() ?? new ApiSecurityOptions();
        if (string.IsNullOrWhiteSpace(security.ApiKey) || security.ApiKey.Length < ApiSecurityOptions.MinimumKeyLength)
        {
            // Fail closed: never start an unauthenticated API.
            throw new InvalidOperationException(
                $"Api:ApiKey is not configured (minimum {ApiSecurityOptions.MinimumKeyLength} characters). Set the environment variable Api__ApiKey, " +
                "use 'dotnet user-secrets set \"Api:ApiKey\" \"<key>\"' in Development, or store secret 'Api--ApiKey' in Azure Key Vault.");
        }

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = security.MaxRequestBodyBytes;
            kestrel.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
        });

        builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = Math.Max(1, security.PermitPerMinute), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return builder;
    }

    /// <summary>
    /// Repositories: PostgreSQL when Persistence:Enabled=true (connection string is a secret: user-secrets, env var
    /// ConnectionStrings__Timetable or Key Vault), otherwise the original in-memory behaviour. Health checks included.
    /// </summary>
    public static WebApplicationBuilder AddTimetableStorage(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var persistence = builder.Configuration.GetSection(PersistenceOptions.SectionName).Get<PersistenceOptions>() ?? new PersistenceOptions();
        var healthChecks = builder.Services.AddHealthChecks();
        if (persistence.Enabled)
        {
            var connectionString = builder.Configuration.GetConnectionString(persistence.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Fail closed: Enabled without a connection string is a deployment error, not a reason to fall back silently.
                throw new InvalidOperationException(PersistenceServiceCollectionExtensions.MissingMessage(persistence.ConnectionStringName));
            }

            builder.Services.AddTimetablePersistence(connectionString, persistence); // AppDbContext + EF Core repositories (scoped)
            builder.Services.AddHostedService<RunRecoveryHostedService>();
            healthChecks.AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });
        }
        else
        {
            var apiOptions = builder.Configuration.GetSection(TimetableApiOptions.SectionName).Get<TimetableApiOptions>() ?? new TimetableApiOptions();
            builder.Services.AddInMemoryTimetableRepository(Math.Max(1, apiOptions.MaxRetainedJobs)); // in-memory repositories (singleton)
        }

        return builder;
    }

    /// <summary>Services (Controller → Service → Repository), controllers, JSON settings and centralized error handling.</summary>
    public static IServiceCollection AddTimetableApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();

        services.AddSingleton<IDatasetProvider, FileDatasetProvider>();      // file-based input, unchanged
        services.AddTimetableServices();

        services.AddRouting(o => o.LowercaseUrls = true);
        services.AddControllers().AddJsonOptions(o =>
        {
            // Same serializer settings as the report / CLI output.
            o.JsonSerializerOptions.PropertyNamingPolicy = TimetableReport.JsonOptions.PropertyNamingPolicy;
            o.JsonSerializerOptions.DefaultIgnoreCondition = TimetableReport.JsonOptions.DefaultIgnoreCondition;
            o.JsonSerializerOptions.MaxDepth = 32;
            foreach (var converter in TimetableReport.JsonOptions.Converters)
            {
                o.JsonSerializerOptions.Converters.Add(converter);
            }
        });
        return services;
    }

    /// <summary>
    /// The service layer (shared by the API and the tests, so they cannot drift). Repositories and
    /// <see cref="IDatasetProvider"/> are registered separately.
    /// </summary>
    public static IServiceCollection AddTimetableServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITimetableJobRunner, TimetableJobRunner>();    // one background CP-SAT solve at a time
        services.AddScoped<ITimetableService, TimetableService>();
        services.AddScoped<ISectionService, SectionService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IConflictService, ConflictService>();
        return services;
    }

    /// <summary>Middleware in the original order: errors → HSTS/HTTPS → security headers → rate limit → authN → authZ.</summary>
    public static WebApplication UseTimetablePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            headers.CacheControl = "no-store";
            await next().ConfigureAwait(false);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>All controllers (API-key protected by the fallback policy) and the anonymous readiness probe.</summary>
    public static WebApplication MapTimetableEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        // Readiness includes the database check when persistence is enabled; writes only "Healthy"/"Unhealthy".
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
        app.MapControllers();
        return app;
    }
}
