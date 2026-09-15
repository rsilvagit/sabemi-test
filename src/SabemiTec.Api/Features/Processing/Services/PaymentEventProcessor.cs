using Microsoft.Extensions.Options;
using SabemiTec.Api.Features.Processing.Repositories;
using SabemiTec.Api.Persistence;

namespace SabemiTec.Api.Features.Processing.Services;

/// <summary>Signals that another worker already reclaimed the item (lease expired and
/// claimed elsewhere) between the claim and the attempt to mark it processed. Benign —
/// not a failure.</summary>
internal sealed class LeaseLostException : Exception;

public sealed class PaymentEventProcessor(
    IContractStatusRepository contracts,
    IOutboxClaimRepository outbox,
    IUnitOfWork uow,
    IOptions<ProcessingOptions> options)
{
    public async Task ProcessAsync(ClaimedPaymentEvent evt, CancellationToken ct)
    {
        // The delay simulates the heavy work and stays OUTSIDE the transaction — holding it
        // open for 2s would pin a pool connection and contract row locks under a parallel batch.
        await Task.Delay(options.Value.SimulatedDelayMs, ct);

        try
        {
            await uow.ExecuteAsync(async innerCt =>
            {
                var isSuccess = string.Equals(evt.PaymentStatus, "PAGO", StringComparison.OrdinalIgnoreCase);

                if (evt.ContractId is not null)
                {
                    await contracts.UpsertAsync(new UpsertContractStatusCommand(
                        ContractId: evt.ContractId,
                        Amount: isSuccess ? (evt.Amount ?? 0) : 0,
                        SuccessDelta: isSuccess ? 1 : 0,
                        FailedDelta: isSuccess ? 0 : 1,
                        PaymentDate: evt.PaymentDate,
                        TransactionId: evt.TransactionId,
                        PaymentStatus: evt.PaymentStatus), innerCt);
                }

                // Conditional on the lease: if another worker already reclaimed the item,
                // affected=0 and we undo the contract upsert instead of applying it twice.
                var affected = await outbox.MarkProcessedAsync(evt.Id, innerCt);
                if (affected == 0)
                {
                    throw new LeaseLostException();
                }

                return affected;
            }, ct);
        }
        catch (LeaseLostException)
        {
            // Nothing to do — the item belongs to another worker now.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // New autocommit connection/scope: if the original failure was a connection
            // issue, the uow's transaction is already unusable.
            await outbox.MarkFailedOrDeadLetteredAsync(
                evt.Id, options.Value.MaxAttempts, options.Value.RetryDelaySeconds, ex.Message, ct);
        }
    }
}
