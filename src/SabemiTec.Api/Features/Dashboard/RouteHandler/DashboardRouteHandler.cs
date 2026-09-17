using SabemiTec.Api.Configurations.Extensions;
using SabemiTec.Api.Configurations.RateLimiting;
using SabemiTec.Api.Features.Dashboard.DTO;
using SabemiTec.Api.Features.Dashboard.Repositories;

namespace SabemiTec.Api.Features.Dashboard.RouteHandler;

public static class DashboardRouteHandler
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .RequireRateLimiting(RateLimitingSetup.DashboardPolicy)
            .RequireCors(ServicesExtensions.DashboardCorsPolicy)
            .WithTags("Dashboard");

        group.MapGet("/", SearchAsync)
            .WithName("SearchPayments")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetPaymentStats")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        group.MapGet("/{id:long}", GetByIdAsync)
            .WithName("GetPaymentById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        // Feeds the "Todos os contratos" filter dropdown in the dashboard UI. Also mapped at
        // GET /webhooks/contracts for SabemiTec.LoadSimulator — see that handler for why.
        group.MapGet("/contracts", GetContractIdsAsync)
            .WithName("ListContractIds")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        // Payloads that failed validation, kept out of / (no dependable contract/transaction
        // reference to show alongside a real payment) but still visible — the PDF requires a
        // clear visual alert for them, just not mixed into the main payments list.
        group.MapGet("/invalid", SearchInvalidAsync)
            .WithName("SearchInvalidPayments")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> SearchAsync(
        IPaymentQueryRepository repository,
        CancellationToken ct,
        string? status = null,
        string? contractId = null,
        string? contractType = null,
        int page = 1,
        int pageSize = 25)
    {
        var query = new PaymentQuery(status, contractId, contractType, page <= 0 ? 1 : page, pageSize <= 0 ? 25 : pageSize);
        var (items, hasMore) = await repository.SearchAsync(query, ct);

        return Results.Ok(new SearchPaymentsResponse(
            items.Select(ToDto).ToList(),
            query.Page,
            query.PageSize,
            hasMore));
    }

    private static async Task<IResult> GetByIdAsync(long id, IPaymentQueryRepository repository, CancellationToken ct)
    {
        var detail = await repository.FindByIdAsync(id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(ToDetailDto(detail));
    }

    private static async Task<IResult> GetStatsAsync(IPaymentQueryRepository repository, CancellationToken ct)
    {
        var stats = await repository.GetStatsAsync(ct);
        return Results.Ok(new PaymentStatsDto(stats.Total, stats.Success, stats.Error, stats.Pending));
    }

    // Internal, not private: also mapped at GET /webhooks/contracts (PaymentWebhookRouteHandler)
    // for SabemiTec.LoadSimulator, which needs real contract ids but has no business holding
    // Dashboard:ApiKey — same handler, same data, exposed under whichever key fits the caller.
    internal static async Task<IResult> GetContractIdsAsync(IPaymentQueryRepository repository, CancellationToken ct)
    {
        var contracts = await repository.ListContractsAsync(ct);
        return Results.Ok(contracts.Select(c => new ContractDto(c.ContractId, c.ContractType, c.Installments, c.TotalValue)).ToList());
    }

    private static async Task<IResult> SearchInvalidAsync(
        IPaymentQueryRepository repository,
        CancellationToken ct,
        int page = 1,
        int pageSize = 25)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 25 : pageSize;

        var (items, hasMore) = await repository.SearchInvalidAsync(page, pageSize, ct);
        var total = await repository.CountInvalidAsync(ct);

        return Results.Ok(new SearchInvalidPaymentsResponse(
            items.Select(ToInvalidDto).ToList(), page, pageSize, hasMore, total));
    }

    private static InvalidPaymentEventDto ToInvalidDto(InvalidPaymentEvent item) => new(
        item.Id,
        item.TransactionId,
        item.ContractId,
        item.Amount,
        item.PaymentDate,
        item.ValidationError,
        item.ReceivedAt);

    private static PaymentListItemDto ToDto(PaymentListItem item) => new(
        item.Id,
        item.TransactionId,
        item.ContractId,
        item.Amount,
        item.PaymentDate,
        item.PaymentStatus,
        item.ProcessingStatus,
        item.EffectiveStatus,
        item.ErrorCategory,
        item.ContractType,
        item.Installments,
        item.TotalValue,
        item.InstallmentNumber,
        item.Attempts,
        item.LastError,
        item.ValidationError,
        item.ReceivedAt,
        item.ProcessedAt);

    private static PaymentDetailDto ToDetailDto(PaymentDetail item) => new(
        item.Id,
        item.TransactionId,
        item.ContractId,
        item.Amount,
        item.PaymentDate,
        item.PaymentStatus,
        item.ProcessingStatus,
        item.EffectiveStatus,
        item.ErrorCategory,
        item.ContractType,
        item.Installments,
        item.TotalValue,
        item.InstallmentNumber,
        item.Attempts,
        item.LastError,
        item.ValidationError,
        item.ReceivedAt,
        item.ProcessedAt,
        item.Payload);
}
