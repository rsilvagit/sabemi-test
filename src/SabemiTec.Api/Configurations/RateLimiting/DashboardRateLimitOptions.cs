namespace SabemiTec.Api.Configurations.RateLimiting;

public sealed class DashboardRateLimitOptions
{
    public const string SectionName = "RateLimiting:Dashboard";

    // Fixed window, not token bucket: the traffic here is a browser polling every 5s, not a
    // partner's burst. 120/min comfortably covers several tabs open at once while still
    // capping a runaway client or a scripted scrape.
    public int PermitLimit { get; set; } = 120;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 0;
}
