namespace SabemiTec.Api.Features.Processing.Repositories;

public sealed record UpsertContractStatusCommand(
    string ContractId,
    decimal Amount,
    int SuccessDelta,
    int FailedDelta,
    DateTimeOffset? PaymentDate,
    string TransactionId,
    string? PaymentStatus);

public interface IContractStatusRepository
{
    Task UpsertAsync(UpsertContractStatusCommand command, CancellationToken ct);
}
