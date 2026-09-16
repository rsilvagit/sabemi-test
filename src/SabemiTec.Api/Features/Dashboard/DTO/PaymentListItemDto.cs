namespace SabemiTec.Api.Features.Dashboard.DTO;

public record PaymentListItemDto(
    long Id,
    string TransactionId,
    string? ContractId,
    decimal? Amount,
    DateTimeOffset? PaymentDate,
    string? PaymentStatus,
    short ProcessingStatus,
    string EffectiveStatus,
    string? ErrorCategory,
    string? ContractType,
    int? Installments,
    int Attempts,
    string? LastError,
    string? ValidationError,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt
);
