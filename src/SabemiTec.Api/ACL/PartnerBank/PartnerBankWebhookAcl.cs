using System.Globalization;
using System.Text.Json;
using SabemiTec.Api.ACL.PartnerBank.DTO;
using SabemiTec.Api.ACL.Responses;
using SabemiTec.Api.Enum;

namespace SabemiTec.Api.ACL.PartnerBank;

public sealed class PartnerBankWebhookAcl : IPaymentWebhookAcl
{
    public PaymentTranslationResult Translate(JsonElement body)
    {
        var rawPayload = body.GetRawText();

        PaymentWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<PaymentWebhookPayload>(rawPayload);
        }
        catch (JsonException)
        {
            payload = null;
        }

        var errors = new List<ValidationFailure>();

        if (payload is null)
        {
            errors.Add(new ValidationFailure("payload", "Body does not match the expected format."));
            return Invalid(rawPayload, null, errors);
        }

        var transactionId = string.IsNullOrWhiteSpace(payload.TransactionId)
            ? $"MISSING:{Guid.NewGuid()}"
            : payload.TransactionId.Trim();

        if (string.IsNullOrWhiteSpace(payload.TransactionId))
        {
            errors.Add(new ValidationFailure("id_transacao", "Required field."));
        }
        else if (payload.TransactionId.Length > 128)
        {
            errors.Add(new ValidationFailure("id_transacao", "Exceeds 128 characters."));
        }

        if (string.IsNullOrWhiteSpace(payload.ContractId))
        {
            errors.Add(new ValidationFailure("id_contrato", "Required field."));
        }

        if (payload.Amount is null || payload.Amount <= 0)
        {
            errors.Add(new ValidationFailure("valor", "Must be numeric and greater than zero."));
        }

        DateTimeOffset? paymentDate = null;
        if (string.IsNullOrWhiteSpace(payload.PaymentDate))
        {
            errors.Add(new ValidationFailure("data_pagamento", "Required field."));
        }
        else if (!TryParseDate(payload.PaymentDate, out paymentDate))
        {
            errors.Add(new ValidationFailure("data_pagamento", "Invalid date."));
        }

        var bankStatus = BankPaymentStatusEnum.TryParse(payload.Status);
        if (string.IsNullOrWhiteSpace(payload.Status))
        {
            errors.Add(new ValidationFailure("status", "Required field."));
        }
        else if (bankStatus is null)
        {
            errors.Add(new ValidationFailure("status", $"Unknown status: '{payload.Status}'."));
        }

        if (errors.Count > 0)
        {
            return new PaymentTranslationResult
            {
                RawPayload = rawPayload,
                TransactionId = transactionId,
                ContractId = payload.ContractId,
                Amount = payload.Amount,
                PaymentDate = paymentDate,
                BankStatus = bankStatus?.Name ?? payload.Status,
                IsValid = false,
                Errors = errors
            };
        }

        return new PaymentTranslationResult
        {
            RawPayload = rawPayload,
            TransactionId = transactionId,
            ContractId = payload.ContractId,
            Amount = payload.Amount,
            PaymentDate = paymentDate,
            BankStatus = bankStatus?.Name,
            IsValid = true,
            Errors = []
        };
    }

    private static PaymentTranslationResult Invalid(string rawPayload, string? transactionId, List<ValidationFailure> errors) =>
        new()
        {
            RawPayload = rawPayload,
            TransactionId = transactionId ?? $"MISSING:{Guid.NewGuid()}",
            IsValid = false,
            Errors = errors
        };

    private static bool TryParseDate(string value, out DateTimeOffset? result)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = null;
        return false;
    }
}
