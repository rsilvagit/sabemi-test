using System.Text.Json;
using SabemiTec.Api.ACL;
using SabemiTec.Api.ACL.Responses;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Enum;
using SabemiTec.Api.Features.Webhooks.Repositories;

namespace SabemiTec.Api.Features.Webhooks.Services;

public enum IngestOutcome
{
    Accepted,
    Duplicate,
    Rejected
}

public sealed record IngestPaymentResult(IngestOutcome Outcome, long? EventId, string TransactionId, IReadOnlyList<ValidationFailure> Errors);

/// <summary>
/// Flow of POST /webhooks/payment: translate via the ACL, do a single idempotent INSERT,
/// and return the outcome. No transaction here on purpose — a single write doesn't need one
/// (see plan section 6.5) — but the connection still needs to be opened first.
/// </summary>
public sealed class IngestPaymentHandler(IPaymentWebhookAcl acl, IPaymentEventRepository repository, IUnitOfWork uow)
{
    public async Task<IngestPaymentResult> HandleAsync(JsonElement body, CancellationToken ct)
    {
        uow.Open();

        var translated = acl.Translate(body);

        var command = new InsertPaymentEventCommand(
            TransactionId: translated.TransactionId!,
            ContractId: translated.ContractId,
            Amount: translated.Amount,
            PaymentDate: translated.PaymentDate,
            PaymentStatus: translated.BankStatus,
            Payload: translated.RawPayload,
            IsValid: translated.IsValid,
            ValidationError: translated.IsValid ? null : string.Join("; ", translated.Errors.Select(e => $"{e.Field}: {e.Message}")),
            ProcessingStatus: (short)(translated.IsValid ? ProcessingStatusEnum.Pending.Id : ProcessingStatusEnum.DeadLettered.Id));

        var insertedId = await repository.InsertIfNotExistsAsync(command, ct);

        if (insertedId is null)
        {
            var existing = await repository.FindByTransactionIdAsync(translated.TransactionId!, ct);
            return new IngestPaymentResult(IngestOutcome.Duplicate, existing?.Id, translated.TransactionId!, []);
        }

        return new IngestPaymentResult(
            translated.IsValid ? IngestOutcome.Accepted : IngestOutcome.Rejected,
            insertedId,
            translated.TransactionId!,
            translated.Errors);
    }
}
