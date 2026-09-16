using System.Text.Json;
using SabemiTec.Api.Features.Webhooks.Services;
using SabemiTec.Api.Configurations.RateLimiting;

namespace SabemiTec.Api.Features.Webhooks.RouteHandler;

public static class PaymentWebhookRouteHandler
{
    public static IEndpointRouteBuilder MapPaymentWebhooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks")
            .RequireRateLimiting(RateLimitingSetup.WebhookPolicy)
            .WithTags("Webhooks");

        group.MapPost("/pagamento", PostPaymentAsync)
            .WithName("PostPayment")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> PostPaymentAsync(
        HttpContext httpContext,
        IngestPaymentHandler handler,
        CancellationToken ct)
    {
        JsonElement body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Request.Body, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Body is not valid JSON.");
        }

        var result = await handler.HandleAsync(body, ct);

        return result.Outcome switch
        {
            IngestOutcome.Accepted => Results.Accepted($"/api/payments/{result.EventId}", new
            {
                id = result.EventId,
                transactionId = result.TransactionId,
                status = "Accepted"
            }),
            IngestOutcome.Duplicate => DuplicateResult(httpContext, result),
            IngestOutcome.Rejected => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid payload.",
                extensions: new Dictionary<string, object?>
                {
                    ["errors"] = result.Errors.Select(e => new { field = e.Field, message = e.Message })
                }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult DuplicateResult(HttpContext httpContext, IngestPaymentResult result)
    {
        httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
        return Results.Ok(new
        {
            id = result.EventId,
            transactionId = result.TransactionId,
            duplicate = true,
            status = "Duplicate"
        });
    }
}
