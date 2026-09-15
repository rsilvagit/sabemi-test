using System.Text.Json;
using FluentAssertions;
using SabemiTec.Api.ACL.PartnerBank;
using Xunit;

namespace SabemiTec.Tests.Unit;

/// <summary>
/// Protects the central requirement: an invalid payload must never throw or disappear —
/// it needs to be translated with IsValid=false so the handler persists it and the
/// dashboard can show the error.
/// </summary>
public class PartnerBankWebhookAclTests
{
    private readonly PartnerBankWebhookAcl _sut = new();

    private static JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void Translate_ValidPayload_ReturnsIsValidTrueWithNoErrors()
    {
        var body = Parse("""
            {
              "id_transacao": "TX-001",
              "id_contrato": "CT-42",
              "valor": 150.75,
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "PAGO"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.TransactionId.Should().Be("TX-001");
        result.ContractId.Should().Be("CT-42");
        result.Amount.Should().Be(150.75m);
        result.BankStatus.Should().Be("PAGO");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Translate_AmountLessThanOrEqualToZero_IsInvalid(decimal amount)
    {
        var body = Parse($$"""
            {
              "id_transacao": "TX-002",
              "id_contrato": "CT-42",
              "valor": {{amount}},
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "PAGO"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "valor");
    }

    [Fact]
    public void Translate_EmptyContractId_IsInvalid()
    {
        var body = Parse("""
            {
              "id_transacao": "TX-003",
              "id_contrato": "",
              "valor": 10,
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "PAGO"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "id_contrato");
    }

    [Fact]
    public void Translate_InvalidDate_IsInvalid()
    {
        var body = Parse("""
            {
              "id_transacao": "TX-004",
              "id_contrato": "CT-42",
              "valor": 10,
              "data_pagamento": "not-a-date",
              "status": "PAGO"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "data_pagamento");
    }

    [Fact]
    public void Translate_UnknownStatus_IsInvalid()
    {
        var body = Parse("""
            {
              "id_transacao": "TX-005",
              "id_contrato": "CT-42",
              "valor": 10,
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "ANYTHING"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Field == "status");
    }

    [Fact]
    public void Translate_MissingTransactionId_GeneratesSyntheticKeyAndPersistsAsInvalid()
    {
        var body = Parse("""
            {
              "id_contrato": "CT-42",
              "valor": 10,
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "PAGO"
            }
            """);

        var result = _sut.Translate(body);

        result.IsValid.Should().BeFalse();
        result.TransactionId.Should().StartWith("MISSING:");
        result.Errors.Should().Contain(e => e.Field == "id_transacao");
    }

    [Theory]
    [InlineData("pago", "PAGO")]
    [InlineData("FALHA", "FALHA")]
    [InlineData(" Pago ", "PAGO")]
    public void Translate_NormalizesStatusVocabulary(string bankStatus, string expected)
    {
        var body = Parse($$"""
            {
              "id_transacao": "TX-006",
              "id_contrato": "CT-42",
              "valor": 10,
              "data_pagamento": "2026-09-15T10:00:00Z",
              "status": "{{bankStatus}}"
            }
            """);

        var result = _sut.Translate(body);

        result.BankStatus.Should().Be(expected);
    }
}
