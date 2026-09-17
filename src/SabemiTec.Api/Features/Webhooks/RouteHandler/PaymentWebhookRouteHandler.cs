using System.Text.Json;
using SabemiTec.Api.ACL.PartnerBank.DTO;
using SabemiTec.Api.Configurations.RateLimiting;
using SabemiTec.Api.Features.Webhooks.DTO;
using SabemiTec.Api.Features.Webhooks.Services;

namespace SabemiTec.Api.Features.Webhooks.RouteHandler;

public static class PaymentWebhookRouteHandler
{
    public static IEndpointRouteBuilder MapPaymentWebhooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks")
            .RequireRateLimiting(RateLimitingSetup.WebhookPolicy)
            .WithTags("Webhooks");

        group.MapPost("/payment", PostPaymentAsync)
            .WithName("PostPayment")
            // Documentation only: the handler still reads the raw JsonElement body itself
            // (see below) so the ACL keeps owning the only place that knows the partner
            // bank's field names — this just gives Swagger a schema to show.
            .Accepts<PaymentWebhookPayload>("application/json")
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
            IngestOutcome.Accepted => Results.Accepted(
                $"/api/payments/{result.EventId}",
                new PaymentAcceptedResponse(result.EventId, result.TransactionId, "Accepted")),
            IngestOutcome.Duplicate => DuplicateResult(httpContext, result),
            IngestOutcome.Rejected => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid payload.",
                extensions: new Dictionary<string, object?> { ["errors"] = result.Errors }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult DuplicateResult(HttpContext httpContext, IngestPaymentResult result)
    {
        httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
        return Results.Ok(new PaymentDuplicateResponse(result.EventId, result.TransactionId, Duplicate: true, Status: "Duplicate"));
    }
}
