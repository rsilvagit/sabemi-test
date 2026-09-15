using System.Text.Json.Serialization;

namespace SabemiTec.Api.ACL.PartnerBank.Dto;

/// <summary>
/// Mirrors the partner bank's contract literally. This is the only place in the codebase
/// that knows "id_transacao". Minimal API uses System.Text.Json (not Newtonsoft), so the
/// snake_case fields require an explicit [JsonPropertyName] — without it they come back
/// null silently.
/// </summary>
public sealed class PaymentWebhookPayload
{
    [JsonPropertyName("id_transacao")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("id_contrato")]
    public string? ContractId { get; set; }

    [JsonPropertyName("valor")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("data_pagamento")]
    public string? PaymentDate { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }
}
