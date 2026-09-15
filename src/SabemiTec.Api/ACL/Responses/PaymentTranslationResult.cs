namespace SabemiTec.Api.ACL.Responses;

public sealed record ValidationFailure(string Field, string Message);

/// <summary>Result of the translation: either a domain-ready event, or the validation errors
/// (which also need to be persisted — see the ACL notes in the plan, section 2).</summary>
public sealed class PaymentTranslationResult
{
    public required string RawPayload { get; init; }
    public string? TransactionId { get; init; }
    public string? ContractId { get; init; }
    public decimal? Amount { get; init; }
    public DateTimeOffset? PaymentDate { get; init; }
    public string? BankStatus { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationFailure> Errors { get; init; } = [];
}
