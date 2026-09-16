namespace SabemiTec.Api.Database.PostgreSQL;

public interface IUnitOfWork : IDisposable
{
    void Open();
    void BeginTransaction();
    void Commit();
    void Rollback();
}
