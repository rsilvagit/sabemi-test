using SabemiTec.Api.Configurations.Extensions;
using SabemiTec.Api.Configurations.RateLimiting;
using SabemiTec.Api.Database.PostgreSQL;
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

        // Demo-only: lets a synthetic traffic generator (SabemiTec.LoadSimulator) target
        // real seeded contracts instead of hardcoding a list that can drift from the DB.
        group.MapGet("/contracts", GetContractIdsAsync)
            .WithName("ListContractIds")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> SearchAsync(
        IPaymentQueryRepository repository,
        IUnitOfWork uow,
        CancellationToken ct,
        string? status = null,
        string? contractId = null,
        int page = 1,
        int pageSize = 25)
    {
        uow.Open();

        var query = new PaymentQuery(status, contractId, page <= 0 ? 1 : page, pageSize <= 0 ? 25 : pageSize);
        var (items, hasMore) = await repository.SearchAsync(query, ct);

        return Results.Ok(new SearchPaymentsResponse(
            items.Select(ToDto).ToList(),
            query.Page,
            query.PageSize,
            hasMore));
    }

    private static async Task<IResult> GetByIdAsync(long id, IPaymentQueryRepository repository, IUnitOfWork uow, CancellationToken ct)
    {
        uow.Open();

        var detail = await repository.FindByIdAsync(id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(ToDetailDto(detail));
    }

    private static async Task<IResult> GetStatsAsync(IPaymentQueryRepository repository, IUnitOfWork uow, CancellationToken ct)
    {
        uow.Open();

        var stats = await repository.GetStatsAsync(ct);
        return Results.Ok(new PaymentStatsDto(stats.Total, stats.Success, stats.Error, stats.Pending));
    }

    private static async Task<IResult> GetContractIdsAsync(IPaymentQueryRepository repository, IUnitOfWork uow, CancellationToken ct)
    {
        uow.Open();

        var contractIds = await repository.ListContractIdsAsync(ct);
        return Results.Ok(contractIds);
    }

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
        item.Attempts,
        item.LastError,
        item.ValidationError,
        item.ReceivedAt,
        item.ProcessedAt,
        item.Payload);
}
