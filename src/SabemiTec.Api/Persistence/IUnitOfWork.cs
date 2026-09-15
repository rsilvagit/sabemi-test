using System.Data.Common;

namespace SabemiTec.Api.Persistence;

/// <summary>
/// Owns the connection. The worker opens a transaction (two writes that must be atomic);
/// the POST and reads use the connection in autocommit — see plan section 6.5.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    DbConnection Connection { get; }
    DbTransaction? Transaction { get; }

    Task<DbConnection> EnsureOpenAsync(CancellationToken ct = default);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);
}
