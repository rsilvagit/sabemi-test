using Microsoft.Extensions.Options;
using SabemiTec.Api.Features.Processing.Repositories;

namespace SabemiTec.Api.Features.Processing.Services;

/// <summary>
/// BackgroundService is a singleton; the unit of work is scoped — so an
/// IServiceScopeFactory creates a scope per ITEM (not per batch), never IUnitOfWork
/// injected straight into the constructor (captive dependency: a connection shared
/// across threads).
/// </summary>
public sealed class PaymentEventProcessorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessingOptions> options,
    ILogger<PaymentEventProcessorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            logger.LogInformation("Worker disabled (Processing:WorkerEnabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.Value.PollIntervalMs));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await ClaimBatchAsync(stoppingToken);

                if (claimed.Count == 0)
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    continue;
                }

                await Parallel.ForEachAsync(
                    claimed,
                    new ParallelOptions { MaxDegreeOfParallelism = options.Value.DegreeOfParallelism, CancellationToken = stoppingToken },
                    async (evt, ct) => await ProcessOneAsync(evt, ct));

                // Full batch: don't wait for the timer, drain the queue under a burst.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // An unhandled exception here would kill the BackgroundService silently —
                // the app would stay up looking healthy while nothing gets processed.
                logger.LogError(ex, "Error in the processing worker loop.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<ClaimedPaymentEvent>> ClaimBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxClaimRepository>();
        return await outbox.ClaimBatchAsync(options.Value.BatchSize, options.Value.LeaseSeconds, ct);
    }

    private async Task ProcessOneAsync(ClaimedPaymentEvent evt, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<PaymentEventProcessor>();
        await processor.ProcessAsync(evt, ct);
    }
}
