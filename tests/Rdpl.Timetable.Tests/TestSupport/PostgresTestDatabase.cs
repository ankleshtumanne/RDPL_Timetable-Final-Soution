using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rdpl.Timetable.Persistence;
using Rdpl.Timetable.Tests.Persistence;

namespace Rdpl.Timetable.Tests.TestSupport;

/// <summary>
/// A uniquely named database created for one test (schema via EnsureCreated) and dropped afterwards.
/// Requires <see cref="PostgresFactAttribute.EnvironmentVariable"/>.
/// </summary>
internal sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private readonly string _name;

    private PostgresTestDatabase(string adminConnectionString, string name, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PostgresTestDatabase> CreateAsync()
    {
        var admin = Environment.GetEnvironmentVariable(PostgresFactAttribute.EnvironmentVariable)!;
        var name = "rdpl_test_" + Guid.NewGuid().ToString("N")[..12];
        var cs = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;
        var database = new PostgresTestDatabase(admin, name, cs);
        await using var db = database.NewContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    public AppDbContext NewContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_adminConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }
}
