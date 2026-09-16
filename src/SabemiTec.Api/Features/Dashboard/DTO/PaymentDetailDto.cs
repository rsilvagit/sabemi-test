namespace SabemiTec.Api.Features.Dashboard.DTO;

public sealed record PaymentDetailDto(
    long Id,
    string TransactionId,
    string? ContractId,
    decimal? Amount,
    DateTimeOffset? PaymentDate,
    string? PaymentStatus,
    short ProcessingStatus,
    string EffectiveStatus,
    int Attempts,
    string? LastError,
    string? ValidationError,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    string RawPayload
);
