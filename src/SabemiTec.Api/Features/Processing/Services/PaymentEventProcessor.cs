using Microsoft.Extensions.Options;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Enum;
using SabemiTec.Api.Features.Processing.Repositories;

namespace SabemiTec.Api.Features.Processing.Services;

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

        uow.Open();
        uow.BeginTransaction();

        try
        {
            var isSuccess = string.Equals(evt.PaymentStatus, BankPaymentStatusEnum.Paid.Name, StringComparison.OrdinalIgnoreCase);

            if (evt.ContractId is not null)
            {
                await contracts.UpsertAsync(new UpsertContractStatusCommand(
                    ContractId: evt.ContractId,
                    Amount: isSuccess ? (evt.Amount ?? 0) : 0,
                    SuccessDelta: isSuccess ? 1 : 0,
                    FailedDelta: isSuccess ? 0 : 1,
                    PaymentDate: evt.PaymentDate,
                    TransactionId: evt.TransactionId,
                    PaymentStatus: evt.PaymentStatus), ct);
            }

            // Conditional on the lease: if another worker already reclaimed the item,
            // affected=0 and we undo the contract upsert instead of applying it twice —
            // benign, not a failure, so no dead-letter write follows.
            var affected = await outbox.MarkProcessedAsync(evt.Id, ct);
            if (affected == 0)
            {
                uow.Rollback();
                return;
            }

            uow.Commit();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            uow.Rollback();

            // Ad-hoc connection: if the original failure was a connection issue, the
            // uow's connection is already unusable.
            await outbox.MarkFailedOrDeadLetteredAsync(
                evt.Id, options.Value.MaxAttempts, options.Value.RetryDelaySeconds, ex.Message, ct);
        }
    }
}
