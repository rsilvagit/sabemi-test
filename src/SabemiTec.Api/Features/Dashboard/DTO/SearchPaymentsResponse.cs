namespace SabemiTec.Api.Features.Dashboard.DTO;

public record SearchPaymentsResponse(
    IReadOnlyList<PaymentListItemDto> Items,
    int Page,
    int PageSize,
    bool HasMore
);
