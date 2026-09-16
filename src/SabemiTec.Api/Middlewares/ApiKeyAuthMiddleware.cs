using System.Security.Cryptography;
using System.Text;

namespace SabemiTec.Api.Middlewares;

/// <summary>
/// Constant-time comparison over fixed-size hashes: eliminates the length leak that
/// FixedTimeEquals over raw bytes would have. A failure never persists anything and never
/// logs the received value. Same shape as core.flashcard-master's
/// IaConvertWebhookAuthMiddleware — a classic middleware that checks the path itself,
/// instead of a Minimal API endpoint filter.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly string _expectedApiKey;

    public ApiKeyAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _expectedApiKey = configuration["Webhook:ApiKey"]
            ?? throw new InvalidOperationException("Webhook:ApiKey is not configured.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWebhookPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(provided) || !IsValid(provided, _expectedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new { title = "Unauthorized.", status = 401 });
            return;
        }

        await _next(context);
    }

    private static bool IsWebhookPath(PathString path) => path.StartsWithSegments("/webhooks/pagamento");

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
