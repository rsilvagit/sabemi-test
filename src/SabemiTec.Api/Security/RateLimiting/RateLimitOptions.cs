namespace SabemiTec.Api.Security.RateLimiting;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting:Webhook";

    // Sized for a burst: a partner bank closes its batch and fires notifications in
    // seconds, then stays quiet for hours. Token bucket lets that burst through while
    // still capping the sustained rate.
    public int TokenLimit { get; set; } = 200;
    public int TokensPerPeriod { get; set; } = 50;
    public int ReplenishmentPeriodSeconds { get; set; } = 1;

    // Deliberately 0: queueing would hold the bank's request waiting for a token, which
    // contradicts the "respond fast" requirement. Better to 429 in 1ms than hold for seconds.
    public int QueueLimit { get; set; } = 0;
}
