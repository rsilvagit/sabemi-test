using Npgsql;
using SabemiTec.Api.Features.Dashboard.RouteHandler;
using SabemiTec.Api.Features.Webhooks.RouteHandler;

namespace SabemiTec.Api.Configurations.Extensions;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder ConfigureMapsApp(this WebApplication app)
    {
        app.MapGet("/health", async (NpgsqlDataSource dataSource, CancellationToken ct) =>
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            return Results.Ok(new { status = "ok" });
        });

        app.MapPaymentWebhooks();
        app.MapDashboardEndpoints();

        return app;
    }
}
