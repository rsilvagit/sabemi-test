using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Features.Dashboard.RouteHandler;
using SabemiTec.Api.Features.Webhooks.RouteHandler;

namespace SabemiTec.Api.Configurations.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder ConfigureMapsApp(this WebApplication app)
    {
        app.MapGet("/health", async (IDatabaseConnection db) =>
        {
            // A trivial query, not just Connection.Open(): proves the DB round-trips, not just
            // that a TCP handshake succeeds. Dapper opens/closes the connection on its own.
            await db.Connection.ExecuteScalarAsync<int>("select 1");
            return Results.Ok(new { status = "ok" });
        })
        .ExcludeFromDescription();

        app.MapPaymentWebhooks();
        app.MapDashboardEndpoints();

        return app;
    }
}
