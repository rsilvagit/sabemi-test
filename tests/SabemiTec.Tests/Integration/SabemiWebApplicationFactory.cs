using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SabemiTec.Tests.Integration;

/// <summary>
/// Worker disabled by default: endpoint tests can't be subject to the worker processing
/// items in the middle of assertions (flakiness). Tests that exercise the worker pass
/// workerEnabled: true and a low/zero SimulatedDelayMs. Rate limit is wide open by default
/// (large token limit) so unrelated tests never trip it; rate limit tests override it low.
/// </summary>
internal sealed class SabemiWebApplicationFactory(
    string connectionString,
    bool workerEnabled = false,
    int simulatedDelayMs = 0,
    int rateLimitTokenLimit = 10_000,
    int rateLimitTokensPerPeriod = 10_000,
    string? webhookApiKey = null,
    string? dashboardApiKey = null) : WebApplicationFactory<Program>
{
    public const string TestApiKey = "test-api-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Webhook:ApiKey"] = webhookApiKey ?? TestApiKey,
                ["Dashboard:ApiKey"] = dashboardApiKey ?? TestApiKey,
                ["Processing:WorkerEnabled"] = workerEnabled.ToString(),
                ["Processing:SimulatedDelayMs"] = simulatedDelayMs.ToString(),
                ["Processing:PollIntervalMs"] = "100",
                ["RateLimiting:Webhook:TokenLimit"] = rateLimitTokenLimit.ToString(),
                ["RateLimiting:Webhook:TokensPerPeriod"] = rateLimitTokensPerPeriod.ToString(),
                ["RateLimiting:Webhook:ReplenishmentPeriodSeconds"] = "1",
                ["RateLimiting:Webhook:QueueLimit"] = "0"
            });
        });
    }
}
