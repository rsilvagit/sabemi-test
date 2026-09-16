namespace SabemiTec.Api.Features.Dashboard.Repositories;

// Class with `init` properties (not a positional record) — same Dapper/timestamptz gotcha
// documented in IPaymentEventRepository.
public class PaymentListItem
{
    public long Id { get; init; }
    public string TransactionId { get; init; } = default!;
    public string? ContractId { get; init; }
    public decimal? Amount { get; init; }
    public DateTimeOffset? PaymentDate { get; init; }
    public string? PaymentStatus { get; init; }
    public short ProcessingStatus { get; init; }
    public string EffectiveStatus { get; init; } = default!;
    public string? ErrorCategory { get; init; }
    public string? ContractType { get; init; }
    public int? Installments { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public string? ValidationError { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset? ProcessedAt { get; init; }
}

public sealed class PaymentDetail : PaymentListItem
{
    public string Payload { get; init; } = default!;
}

public sealed record PaymentQuery(string? Status, string? ContractId, string? ContractType, int Page, int PageSize);

public sealed class PaymentStats
{
    public long Total { get; init; }
    public long Success { get; init; }
    public long Error { get; init; }
    public long Pending { get; init; }
}

public interface IPaymentQueryRepository
{
    /// <returns>Up to PageSize items, plus whether more exist beyond this page.</returns>
    Task<(IReadOnlyList<PaymentListItem> Items, bool HasMore)> SearchAsync(PaymentQuery query, CancellationToken ct);

    Task<PaymentDetail?> FindByIdAsync(long id, CancellationToken ct);

    Task<PaymentStats> GetStatsAsync(CancellationToken ct);

    /// <summary>The known contract IDs (demo master data, see migration 0002) — lets clients
    /// that generate synthetic traffic (SabemiTec.LoadSimulator) target real contracts
    /// instead of hardcoding a list that can drift from what is actually seeded.</summary>
    Task<IReadOnlyList<string>> ListContractIdsAsync(CancellationToken ct);
}
