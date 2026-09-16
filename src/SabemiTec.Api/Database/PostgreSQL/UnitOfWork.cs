using System.Data;

namespace SabemiTec.Api.Database.PostgreSQL;

// Same shape as core.flashcard-master's UnitOfWork: the caller drives Open/BeginTransaction/
// Commit/Rollback explicitly instead of a wrapper that runs a delegate inside a transaction.
public sealed class UnitOfWork(IDatabaseConnection databaseConnection) : IUnitOfWork
{
    private bool _isCommitted;

    public void Open()
    {
        if (databaseConnection.Connection.State != ConnectionState.Open)
        {
            databaseConnection.Connection.Open();
        }
    }

    public void BeginTransaction() => databaseConnection.Transaction = databaseConnection.Connection.BeginTransaction();

    public void Commit()
    {
        databaseConnection.Transaction!.Commit();
        _isCommitted = true;
        Dispose();
    }

    public void Rollback()
    {
        if (!_isCommitted && databaseConnection.Transaction?.Connection != null)
        {
            databaseConnection.Transaction.Rollback();
            Dispose();
        }
    }

    public void Dispose() => databaseConnection.Transaction?.Dispose();
}
