using System.Data;

namespace SabemiTec.Api.Database.PostgreSQL;

public interface IDatabaseConnection : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; set; }
}
