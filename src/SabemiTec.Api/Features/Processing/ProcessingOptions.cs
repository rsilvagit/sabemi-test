namespace SabemiTec.Api.Features.Processing;

public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    public bool WorkerEnabled { get; set; } = true;
    public int SimulatedDelayMs { get; set; } = 2000;
    public int BatchSize { get; set; } = 10;
    public int LeaseSeconds { get; set; } = 120;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public int DegreeOfParallelism { get; set; } = 4;
    public int PollIntervalMs { get; set; } = 500;
}
