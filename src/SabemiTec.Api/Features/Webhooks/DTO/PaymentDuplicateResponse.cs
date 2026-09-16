namespace SabemiTec.Api.Features.Webhooks.DTO;

public sealed record PaymentDuplicateResponse(long? Id, string TransactionId, bool Duplicate, string Status);
