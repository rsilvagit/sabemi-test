using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SabemiTec.Api.Configurations.RateLimiting;

public static class RateLimitingSetup
{
    public const string WebhookPolicy = "webhook";
    public const string DashboardPolicy = "dashboard";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<DashboardRateLimitOptions>(configuration.GetSection(DashboardRateLimitOptions.SectionName));

        services.AddRateLimiter(rl =>
        {
            // Options resolved lazily from HttpContext.RequestServices, at request time —
            // not captured eagerly here. Same class of bug as a singleton capturing
            // IConfiguration at registration time: WebApplicationFactory's test overrides
            // never get seen (they land during Build(), which runs after this).
            rl.AddPolicy(WebhookPolicy, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

                return RateLimitPartition.GetTokenBucketLimiter(
                    ApiKeyOrIpPartitionKey(httpContext),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = options.TokenLimit,
                        TokensPerPeriod = options.TokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(options.ReplenishmentPeriodSeconds),
                        AutoReplenishment = true,
                        QueueLimit = options.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            // Fixed window, partitioned by IP only — every dashboard client shares the same
            // ApiKey (the reverse proxy injects it, see ApiKeyAuthMiddleware), so partitioning
            // by key here would lump every browser into one global bucket instead of one per
            // client.
            rl.AddPolicy(DashboardPolicy, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<DashboardRateLimitOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"ip:{httpContext.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueLimit = options.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            rl.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { title = "Too many requests. Please try again shortly.", status = 429 }, ct);
            };
        });

        return services;
    }

    // Partitioned by ApiKey (hashed — never the raw key in a partition name that ends up in
    // logs/metrics), with an IP fallback so unauthenticated traffic (a brute-force attempt)
    // can't drain the legitimate partner's bucket. Never partition by transaction/contract
    // id: the middleware runs before the payload is even parsed, and unbounded cardinality
    // would leak memory disguised as a feature.
    private static string ApiKeyOrIpPartitionKey(HttpContext httpContext)
    {
        var apiKey = httpContext.Request.Headers["X-Api-Key"].ToString();

        if (!string.IsNullOrEmpty(apiKey))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)))[..16];
            return $"key:{hash}";
        }

        return $"ip:{httpContext.Connection.RemoteIpAddress}";
    }
}
