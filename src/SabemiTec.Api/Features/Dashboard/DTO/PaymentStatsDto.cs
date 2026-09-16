namespace SabemiTec.Api.Features.Dashboard.DTO;

public record PaymentStatsDto(long Total, long Success, long Error, long Pending);
