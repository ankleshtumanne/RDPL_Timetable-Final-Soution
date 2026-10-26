using Rdpl.Timetable.Api.Infrastructure;

// Composition root only. Every endpoint lives in Controllers/; registrations live in Infrastructure/ApiHostingExtensions.
// Request flow: Controller → Service interface → Service → Repository interface → Repository → AppDbContext → PostgreSQL
// (or the in-memory repositories when Persistence:Enabled is false).
var builder = WebApplication.CreateBuilder(args);

builder.AddTimetableConfiguration()   // options + optional Azure Key Vault
       .AddTimetableSecurity()        // API key (fail closed), rate limiting, Kestrel limits
       .AddTimetableStorage();        // PostgreSQL or in-memory repositories, health checks
builder.Services.AddTimetableApplication(); // services, controllers, JSON, centralized exception handling
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Enter your API key",
        Name = "X-API-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});
var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseTimetablePipeline();           // exception handler, HSTS/HTTPS, security headers, rate limiter, authN/authZ
app.MapTimetableEndpoints();          // controllers + /health/ready

app.Run();

/// <summary>Exposed for WebApplicationFactory-based tests.</summary>
public partial class Program
{
}
