using System.Security.Cryptography;
using System.Text;

namespace SabemiTec.Api.Middlewares;

/// <summary>
/// Two separate keys, same header (X-Api-Key): Webhook:ApiKey is meant only for the partner
/// bank, Dashboard:ApiKey only for the dashboard (injected server-side by nginx or baked into
/// the JS bundle — see web/nginx.conf.template). Keeping them distinct means a leak of one
/// (e.g. the dashboard's, which ends up client-side one way or another) never grants access
/// to the other. Constant-time comparison over fixed-size hashes: eliminates the length leak
/// that FixedTimeEquals over raw bytes would have. A failure never persists anything and
/// never logs the received value. Same shape as core.flashcard-master's
/// IaConvertWebhookAuthMiddleware — a classic middleware that checks the path itself,
/// instead of a Minimal API endpoint filter.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string _webhookApiKey;
    private readonly string _dashboardApiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _webhookApiKey = configuration["Webhook:ApiKey"]
            ?? throw new InvalidOperationException("Webhook:ApiKey is not configured.");
        _dashboardApiKey = configuration["Dashboard:ApiKey"]
            ?? throw new InvalidOperationException("Dashboard:ApiKey is not configured.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var expectedApiKey = ExpectedApiKeyFor(context.Request.Path);
        if (expectedApiKey is null)
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(provided) || !IsValid(provided, expectedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new { title = "Unauthorized.", status = 401 });
            return;
        }

        await _next(context);
    }

    private string? ExpectedApiKeyFor(PathString path)
    {
        // Everything under /webhooks (including /webhooks/contracts, used by
        // SabemiTec.LoadSimulator to look up real contract ids) takes the partner bank's
        // key — none of it is ever called from a browser, so it never needs Dashboard:ApiKey.
        if (path.StartsWithSegments("/webhooks"))
        {
            return _webhookApiKey;
        }

        if (path.StartsWithSegments("/api/payments"))
        {
            return _dashboardApiKey;
        }

        return null;
    }

    private static bool IsValid(string provided, string expected)
    {
        Span<byte> a = stackalloc byte[32];
        Span<byte> b = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(provided), a);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), b);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}

public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder) =>
        builder.UseMiddleware<ApiKeyAuthMiddleware>();
}
