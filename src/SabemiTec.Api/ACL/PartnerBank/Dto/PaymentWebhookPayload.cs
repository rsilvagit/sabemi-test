using System.Text.Json.Serialization;

namespace SabemiTec.Api.ACL.PartnerBank.Dto;

/// <summary>
/// Mirrors the partner bank's contract literally. This is the only place in the codebase
/// that knows "id_transacao". Minimal API uses System.Text.Json (not Newtonsoft), so the
/// snake_case fields require an explicit [JsonPropertyName] — without it they come back
/// null silently.
/// </summary>
public sealed record PaymentWebhookPayload(
    [property: JsonPropertyName("id_transacao")] string? TransactionId,
    [property: JsonPropertyName("id_contrato")] string? ContractId,
    [property: JsonPropertyName("valor")] decimal? Amount,
    [property: JsonPropertyName("data_pagamento")] string? PaymentDate,
    [property: JsonPropertyName("status")] string? Status
);
