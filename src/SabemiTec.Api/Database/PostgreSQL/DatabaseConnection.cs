using System.Data;
using Npgsql;

namespace SabemiTec.Api.Database.PostgreSQL;

public sealed class DatabaseConnection : IDatabaseConnection
{
    public IDbConnection Connection { get; }
    public IDbTransaction? Transaction { get; set; }

    public DatabaseConnection(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
        Connection = new NpgsqlConnection(connectionString);
    }

    public void Dispose() => Connection?.Dispose();
}
