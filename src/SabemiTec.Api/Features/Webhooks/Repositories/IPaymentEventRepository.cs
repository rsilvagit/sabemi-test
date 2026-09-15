namespace SabemiTec.Api.Features.Webhooks.Repositories;

public sealed record InsertPaymentEventCommand(
    string TransactionId,
    string? ContractId,
    decimal? Amount,
    DateTimeOffset? PaymentDate,
    string? PaymentStatus,
    string Payload,
    bool IsValid,
    string? ValidationError,
    short ProcessingStatus);

// A class with `init` properties (not a positional record): Dapper maps by property,
// avoiding the constructor matching that fails when timestamptz comes back as DateTime
// from the reader but the property is DateTimeOffset.
public sealed class ExistingPaymentEvent
{
    public long Id { get; init; }
    public string TransactionId { get; init; } = default!;
    public short ProcessingStatus { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
}

public interface IPaymentEventRepository
{
    /// <returns>The id when it inserted now; null when it already existed (duplicate).</returns>
    Task<long?> InsertIfNotExistsAsync(InsertPaymentEventCommand command, CancellationToken ct);

    Task<ExistingPaymentEvent?> FindByTransactionIdAsync(string transactionId, CancellationToken ct);
}
