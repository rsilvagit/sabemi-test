namespace SabemiTec.Api.Features.Dashboard.DTO;

public record ContractDto(
    string ContractId,
    string ContractType,
    int? Installments,
    decimal? TotalValue
);
