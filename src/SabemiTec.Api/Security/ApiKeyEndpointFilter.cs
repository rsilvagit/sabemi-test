using System.Security.Cryptography;
using System.Text;

namespace SabemiTec.Api.Security;

/// <summary>
/// Constant-time comparison over fixed-size hashes: eliminates the length leak that
/// FixedTimeEquals over raw bytes would have. A failure never persists anything and never
/// logs the received value.
/// </summary>
public sealed class ApiKeyEndpointFilter(IConfiguration configuration) : IEndpointFilter
{
    private const string HeaderName = "X-Api-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = configuration["Webhook:ApiKey"];
        if (string.IsNullOrEmpty(expected))
        {
            throw new InvalidOperationException("Webhook:ApiKey is not configured.");
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(provided) || !IsValid(provided, expected))
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized.");
        }

        return await next(context);
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
