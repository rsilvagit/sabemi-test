namespace SabemiTec.Api.Features.Webhooks.DTO;

public sealed record PaymentAcceptedResponse(long? Id, string TransactionId, string Status);
