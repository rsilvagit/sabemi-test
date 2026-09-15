namespace SabemiTec.Api.Features.Processing.Repositories;

// A class with `init` properties (not a positional record) — see the Dapper gotcha with
// timestamptz vs DateTime documented in IPaymentEventRepository.
public sealed class ClaimedPaymentEvent
{
    public long Id { get; init; }
    public string TransactionId { get; init; } = default!;
    public string? ContractId { get; init; }
    public decimal? Amount { get; init; }
    public DateTimeOffset? PaymentDate { get; init; }
    public string? PaymentStatus { get; init; }
    public int Attempts { get; init; }
}

public interface IOutboxClaimRepository
{
    Task<IReadOnlyList<ClaimedPaymentEvent>> ClaimBatchAsync(int batchSize, int leaseSeconds, CancellationToken ct);
    Task<int> MarkProcessedAsync(long id, CancellationToken ct);
    Task MarkFailedOrDeadLetteredAsync(long id, int maxAttempts, int retryDelaySeconds, string error, CancellationToken ct);
}
