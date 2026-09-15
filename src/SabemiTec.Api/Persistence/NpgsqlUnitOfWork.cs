using System.Data.Common;
using Npgsql;

namespace SabemiTec.Api.Persistence;

public sealed class NpgsqlUnitOfWork(NpgsqlDataSource dataSource) : IUnitOfWork
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public DbConnection Connection => _connection ??= dataSource.CreateConnection();
    public DbTransaction? Transaction => _transaction;

    public async Task<DbConnection> EnsureOpenAsync(CancellationToken ct = default)
    {
        if (Connection.State != System.Data.ConnectionState.Open)
        {
            await ((NpgsqlConnection)Connection).OpenAsync(ct);
        }

        return Connection;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already in progress on this Unit of Work.");
        }

        _transaction = await ((NpgsqlConnection)Connection).BeginTransactionAsync(ct);
        try
        {
            var result = await work(ct);
            await _transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await _transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
