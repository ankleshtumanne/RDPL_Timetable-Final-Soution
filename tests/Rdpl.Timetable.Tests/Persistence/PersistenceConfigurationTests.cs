using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Persistence.Options;

namespace Rdpl.Timetable.Tests.Persistence;

/// <summary>Connection-string and options handling: fail closed, never leak the password.</summary>
public sealed class PersistenceConfigurationTests
{
    private const string Secret = "S3cr3t-Pa55word!";

    [Fact]
    public void Defaults_are_valid_and_persistence_is_off()
    {
        var options = new PersistenceOptions();
        Assert.False(options.Enabled);
        Assert.Empty(options.Validate());
    }

    [Theory]
    [InlineData(0, 3, 10)]
    [InlineData(601, 3, 10)]
    [InlineData(60, 11, 10)]
    [InlineData(60, 3, 0)]
    public void Out_of_range_options_are_rejected(int timeout, int retries, int delay)
    {
        var options = new PersistenceOptions { CommandTimeoutSeconds = timeout, MaxRetryCount = retries, MaxRetryDelaySeconds = delay };
        Assert.NotEmpty(options.Validate());
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTimetablePersistence($"Host=h;Database=d;Username=u;Password={Secret}", options));
        Assert.Contains("Persistence:", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_connection_string_fails_closed_with_setup_instructions(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTimetablePersistence(value, new PersistenceOptions()));
        Assert.Contains("ConnectionStrings:Timetable", ex.Message);
        Assert.Contains("user-secrets", ex.Message);
    }

    [Theory]
    [InlineData("Host=h;Database=d;Username=u;Password=" + Secret + ";NotAKeyword=1")]
    [InlineData("Database=d;Username=u;Password=" + Secret)]
    [InlineData("Host=h;Database=d;Username=u;Password=" + Secret + ";Include Error Detail=true")]
    public void Invalid_connection_strings_are_rejected_without_echoing_the_password(string value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddTimetablePersistence(value, new PersistenceOptions()));
        Assert.DoesNotContain(Secret, ex.Message);
    }

    [Fact]
    public void Safe_description_never_contains_the_password()
    {
        var text = PersistenceServiceCollectionExtensions.DescribeSafely($"Host=db.internal;Port=5433;Database=rdpl;Username=app;Password={Secret}");
        Assert.Equal("Host=db.internal;Port=5433;Database=rdpl;Username=app", text);
        Assert.DoesNotContain(Secret, text);
    }

    [Fact]
    public void Valid_configuration_registers_a_scoped_context_with_retry_and_timeout()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddTimetablePersistence($"Host=localhost;Database=rdpl;Username=app;Password={Secret}", new PersistenceOptions { CommandTimeoutSeconds = 45, MaxRetryCount = 2 });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(45, db.Database.GetCommandTimeout());
        Assert.True(db.Database.IsNpgsql());
        Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
