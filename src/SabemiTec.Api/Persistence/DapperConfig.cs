namespace SabemiTec.Api.Persistence;

/// <summary>
/// Called once by Program.cs and by the integration test fixture.
/// Without this, snake_case columns come back null silently.
/// </summary>
public static class DapperConfig
{
    public static void Configure()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
}
