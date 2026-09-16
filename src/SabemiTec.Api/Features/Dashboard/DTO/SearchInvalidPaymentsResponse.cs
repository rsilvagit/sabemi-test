namespace SabemiTec.Api.Features.Dashboard.DTO;

public record SearchInvalidPaymentsResponse(
    IReadOnlyList<InvalidPaymentEventDto> Items,
    int Page,
    int PageSize,
    bool HasMore,
    long Total
);
