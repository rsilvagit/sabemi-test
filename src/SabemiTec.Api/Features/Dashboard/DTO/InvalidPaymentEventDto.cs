namespace SabemiTec.Api.Features.Dashboard.DTO;

public record InvalidPaymentEventDto(
    long Id,
    string TransactionId,
    string? ContractId,
    decimal? Amount,
    DateTimeOffset? PaymentDate,
    string? ValidationError,
    DateTimeOffset ReceivedAt
);
