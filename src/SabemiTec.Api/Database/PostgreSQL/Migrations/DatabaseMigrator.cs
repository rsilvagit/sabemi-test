using System.Reflection;
using DbUp;
using Npgsql;

namespace SabemiTec.Api.Database.PostgreSQL.Migrations;

/// <summary>
/// Applies the schema with DbUp on startup. Compose brings up a single API instance,
/// so the advisory lock is a cheap safeguard, not a requirement driven by replicas.
/// </summary>
public static class DatabaseMigrator
{
    private const long AdvisoryLockKey = 728_411_001L;

    public static async Task MigrateAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        await WaitForDatabaseAsync(connectionString, logger, ct);

        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync(ct);
        await using (var lockCmd = new NpgsqlCommand("select pg_advisory_lock(@key)", lockConnection))
        {
            lockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
            await lockCmd.ExecuteNonQueryAsync(ct);
        }

        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
            {
                throw new InvalidOperationException("Failed to apply migrations.", result.Error);
            }

            logger.LogInformation("Migrations applied: {Count} script(s) executed.", result.Scripts.Count());
        }
        finally
        {
            await using var unlockCmd = new NpgsqlCommand("select pg_advisory_unlock(@key)", lockConnection);
            unlockCmd.Parameters.AddWithValue("key", AdvisoryLockKey);
            await unlockCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task WaitForDatabaseAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
                logger.LogWarning("Postgres is not ready yet, retrying in 1s...");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }

        throw new TimeoutException("Postgres did not become available in time.", lastError);
    }
}
