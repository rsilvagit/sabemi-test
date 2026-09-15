using Microsoft.Extensions.Logging.Abstractions;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Persistence.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace SabemiTec.Tests.Integration;

/// <summary>
/// One container for the whole suite, not per test (~5s startup, paid once).
/// Testcontainers is the only way to genuinely test what matters here: ON CONFLICT,
/// FOR UPDATE SKIP LOCKED, and the cumulative upsert have no in-memory equivalent.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("sabemi")
        .WithUsername("sabemi")
        .WithPassword("sabemi")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        DapperConfig.Configure();
        await _container.StartAsync();
        await DatabaseMigrator.MigrateAsync(ConnectionString, NullLogger.Instance, CancellationToken.None);
    }

    public async Task ResetAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand(
            "truncate payment_event, contract_status restart identity cascade;", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
